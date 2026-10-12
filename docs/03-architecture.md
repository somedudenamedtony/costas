# 03 · Architecture

## Shape

A headless core owns audio, timing, the radio, station state and contact state. The UI renders that state and sends commands. Transmit timing never depends on the UI thread.

```
                +-------------------- App (Avalonia, MVVM) --------------------+
                |  views <- view models <- AppState (observable snapshots)     |
                +-------------------------------^------------------------------+
                                                | commands down, events up
+-----------------------------------------------+------------------------------------------+
|                                   Session (orchestrator)                                 |
|  SlotClock -> AudioCapture -> SlotRecorder -> DecoderHost(jt9) -> MessageParser          |
|                                                         |                                |
|                               StationTracker <----------+----> ContactEngine             |
|                                     |                               |                    |
|        NeedResolver(LogIndex) --> Ranker                   TxScheduler -> Encoder        |
|        HearsMe(PskFeed) -------/                                |          -> AudioPlayback
|                                                                 +-> RigService (rigctld) |
|  LogService(SQLite) <-> QrzLogbook      PskReporter(feed, query, upload)    UdpBroadcaster|
+------------------------------------------------------------------------------------------+
```

## Projects

| Project | Responsibility | May reference |
| --- | --- | --- |
| `Ft8Client.Core` | Messages, callsigns, grids, country file, station tracker, need tiers, ranking, contact engine, FT8/FT4 encoder and waveform maths. Pure logic | nothing |
| `Ft8Client.Audio` | `IAudioBackend`, WASAPI implementation, resampler, ring buffers, `SlotClock`, `SlotRecorder`, `TxPlayer` | Core |
| `Ft8Client.Decoding` | `IDecoder`, `Jt9Decoder` (process host), output parser, WAV writer | Core |
| `Ft8Client.Rig` | `IRig`, `RigctldRig` (process host and TCP client), `SimulatedRig`, serial port scan | Core |
| `Ft8Client.Data` | SQLite schema and migrations, repositories, ADIF reader and writer, settings, secret store, `LogIndex` | Core |
| `Ft8Client.Integrations` | QRZ logbook client, QRZ XML lookup, PSK Reporter feed, query and uploader, WSJT-X UDP broadcaster, SNTP check | Core, Data |
| `Ft8Client.App` | Avalonia views and view models, `Session` composition, simulation wiring | all |

## Key interfaces (in Core unless noted)

```csharp
public interface IDecoder {            // Decoding
    Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct);
}
public sealed record SlotAudio(DateTime SlotStartUtc, Mode Mode, short[] Samples12k);
public sealed record DecodeContext(string MyCall, string MyGrid, string? DxCall, string? DxGrid,
                                   int LowHz, int HighHz, int Depth, int QsoProgress);
public sealed record Decode(DateTime SlotStartUtc, int Snr, double Dt, int OffsetHz,
                            string Text, bool LowConfidence);

public interface IRig {                // Rig
    Task<RigState> GetStateAsync(CancellationToken ct);      // frequency, mode, ptt, split
    Task SetFrequencyAsync(long hz, CancellationToken ct);
    Task SetModeAsync(RigMode mode, CancellationToken ct);
    Task SetPttAsync(bool on, CancellationToken ct);
    Task SetSplitAsync(bool on, long txHz, CancellationToken ct);
    event EventHandler<RigFault> Faulted;
}

public interface IAudioBackend {       // Audio
    IReadOnlyList<AudioDevice> ListInputs();
    IReadOnlyList<AudioDevice> ListOutputs();
    IAudioInput OpenInput(string deviceId, int sampleRate);
    IAudioOutput OpenOutput(string deviceId, int sampleRate);
}

public interface IClock { DateTime UtcNow { get; } long Ticks { get; } }   // injectable for tests
```

`Session` exposes commands (`CallStation`, `CallCq`, `AnswerCaller`, `Resend`, `LogNow`, `Abandon`, `HaltTx`, `SetBand`, `SetMode`) and publishes immutable snapshots (`BandSnapshot`, `ContactSnapshot`, `SignalSnapshot`, `StatusSnapshot`) on change. View models subscribe and marshal to the UI thread.

## Slot pipeline

For FT8 (15 s slots; FT4 uses the same pipeline with 7.5 s):

| Time in slot | What happens |
| --- | --- |
| 0.0 s | `SlotClock` raises SlotStart. `SlotRecorder` starts a new 12 kHz buffer. If a transmission is scheduled for this slot, `TxScheduler` asserts PTT at 0.5 s minus the PTT lead (default 0.2 s) |
| 0.5 s | Tx audio starts (pre-rendered before the slot) |
| 13.14 s | Tx audio ends; PTT released after a 0.1 s tail |
| 13.6 s | Capture cut-off. The buffer is zero-padded to the full slot length and handed to `DecoderHost` |
| about 14.5 s | Decodes arrive. `MessageParser` → `StationTracker` → `ContactEngine` decides the next transmission → `Ranker` recomputes → snapshots published |
| 15.0 s | Next slot |

The decision for the next slot must be made before it starts. If decodes arrive late, the contact engine may still start a transmission up to 1.0 s late (configurable); later than that, it skips the slot. When the operator calls a station or starts CQ during a slot of the right parity, the transmission starts in that slot: up to 1.0 s after the Tx start the whole message goes out, shifted later; from then until 3.0 s after the Tx start it keeps its place in the slot and the part already past is left out, as WSJT-X does. Measured with `ft8sim` and `jt9`, a message missing its first 3 s still decodes at full sensitivity down to about -18 dB (8 in 10 at -20 dB, 1 in 10 at 4 s missing). Later than that, it goes out in the next slot of that parity. Decodes belonging to an earlier slot never trigger a transmission.

The full decode runs `jt9` at depth 3, which takes 3 s or more on a busy band, so its results arrive after the next slot has begun. So that my next message is chosen on time, a quick decode runs alongside it while a contact is in progress (only the other station's frequency, ±100 Hz, at depth 3 with a-priori decoding, typically 0.2 to 0.3 s once `jt9` has its FFT wisdom; without `jt9_wisdom.dat` every run spends about 0.9 s planning FFTs first, so the decoder keeps the file in its temp root and copies it into each run's scratch folder) or while calling CQ (the whole band at depth 1, about 0.5 s). Its results go to the contact engine only; the station list, raw decodes and everything else wait for the full decode. Each message reaches the contact engine once, whichever decode finds it first, so a second copy is not taken for the other station repeating itself.

If the quick decode of the previous slot has not finished when my slot starts, the transmission is held until it has, up to the late-start limit, and then goes out with the message already prepared. The full decode is never waited for.

When transmitting in a slot, nothing is recorded or decoded for that slot.

## Threads

- **Audio threads** (WASAPI callbacks): copy samples to or from a single-producer single-consumer ring buffer. No allocation, locks, logging or exceptions.
- **Slot thread**: one dedicated thread drives `SlotClock` with a high-resolution timer and schedules PTT and Tx start. It never waits on I/O.
- **Worker tasks**: decoding, rig polling (1 s), QRZ, PSK Reporter, database writes. All async, all cancellable.
- **UI thread**: rendering only. Snapshots are immutable, so no locks are shared with the core.

## Processes

| Process | Started | Supervised how |
| --- | --- | --- |
| `jt9` | Once per slot in file mode (v1) | Timeout of one slot length; kill and report on overrun; non-zero exit recorded |
| `rigctld` | At session start when a radio is configured | TCP health check each poll; restart with back-off 1, 2, 5, 10 s; on fault drop PTT via a fresh connection or serial line, stop Tx, raise `RigFault` |

All child processes are placed in a Windows job object so they die with the app.

## Simulation mode

Required from Milestone 0 so that everything can be built and tested without a radio. It is never on by default (decision D9): an installed app with no radio set up shows no data, says "No radio set up" and refuses to transmit. Simulation runs only with `--simulate` or in developer mode.

- Developer mode is hidden: click the product name in About seven times to toggle it. It adds a Developer section to Settings (replay the sample recordings, with or without the simulated partner) and lists Hamlib's test radios (Dummy, models 1 and 6) in Setup. With a test radio selected, transmit audio goes to a null output so a radio with VOX on cannot be keyed by a test.

- `--simulate <folder>` replaces the audio input with a player that feeds WAV files from the folder slot by slot, aligned to the real slot clock, looping.
- `SimulatedRig` implements `IRig` in memory and logs PTT transitions with timestamps.
- Audio output goes to a null sink that records what would have been sent, for assertions.
- `--simulate-partner` adds a scripted partner: when the app transmits to a station, the simulator injects that station's next message into the following slot (by synthesising it with the encoder and mixing it into the input), so full contacts can run end to end.
- A fake PSK Reporter feed and a fake QRZ server (in-process HTTP listener) are used in integration tests and can be enabled in simulation.

## Remote page

`App/Remote`: `RemoteControlService` runs Kestrel (ASP.NET Core) inside the app while Settings has it on. `GET /` serves one embedded HTML page, `POST /api/pair` swaps the one-time code for a device token, and `/ws` carries `RemoteState` (built from `SessionSnapshot` by `RemoteStateBuilder`, reusing the Operate view models' wording) down and commands up. `RemoteControl` holds the rules (pairing, sign-in, `ControlLease`, commands mapped onto `IRemoteTarget`, which `Session` implements) and is tested without the network. Requests from outside private, link-local and CGNAT ranges get 403; WebSockets from another origin are refused. Token hashes go in the OS credential store. The lease check runs every 250 ms on its own task and calls `Session.HaltAsync(reason)` when the controlling phone is silent for 15 s.

## Configuration and storage

Per-user data lives in `%LOCALAPPDATA%\Costas\` (on other platforms, the equivalent app-data folder): `costas.db`, `settings.json`, `logs\`, `wav\`, `cache\`. See `06-data-model.md`.

## Error handling

- Faults are values, not dialogs. Each service exposes a status (`Ok`, `Degraded`, `Down`, with a message) shown in the bottom-bar indicators and Diagnostics.
- Any fault in the rig or audio output during a transmission releases PTT first, then reports.
- Network services retry with exponential back-off, capped at 5 minutes, with jitter.
- Unhandled exceptions are logged, PTT is released, and the app shows a single recoverable error page.

## Logging

Serilog, rolling daily files, 14 days kept. Levels: Information for slot summaries and state changes, Debug for protocol traffic. Never log secrets. A separate plain-text decode journal (see `06-data-model.md`) is not a debug log.

## Portability rules

Windows-only code is confined to: `WasapiAudioBackend`, the credential store implementation, the job-object helper and the installer. Everything else must compile and run on macOS and Linux.
