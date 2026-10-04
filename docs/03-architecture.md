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

The decision for the next slot must be made before it starts. If decodes arrive late, the contact engine may still start a transmission up to 1.0 s late (configurable); later than that, it skips the slot. Decodes belonging to an earlier slot never trigger a transmission.

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

Required from Milestone 0 so that everything can be built and tested without a radio.

- `--simulate <folder>` replaces the audio input with a player that feeds WAV files from the folder slot by slot, aligned to the real slot clock, looping.
- `SimulatedRig` implements `IRig` in memory and logs PTT transitions with timestamps.
- Audio output goes to a null sink that records what would have been sent, for assertions.
- `--simulate-partner` adds a scripted partner: when the app transmits to a station, the simulator injects that station's next message into the following slot (by synthesising it with the encoder and mixing it into the input), so full contacts can run end to end.
- A fake PSK Reporter feed and a fake QRZ server (in-process HTTP listener) are used in integration tests and can be enabled in simulation.

## Configuration and storage

Per-user data lives in `%LOCALAPPDATA%\Ft8Client\` (on other platforms, the equivalent app-data folder): `ft8client.db`, `settings.json`, `logs\`, `wav\`, `cache\`. See `06-data-model.md`.

## Error handling

- Faults are values, not dialogs. Each service exposes a status (`Ok`, `Degraded`, `Down`, with a message) shown in the bottom-bar indicators and Diagnostics.
- Any fault in the rig or audio output during a transmission releases PTT first, then reports.
- Network services retry with exponential back-off, capped at 5 minutes, with jitter.
- Unhandled exceptions are logged, PTT is released, and the app shows a single recoverable error page.

## Logging

Serilog, rolling daily files, 14 days kept. Levels: Information for slot summaries and state changes, Debug for protocol traffic. Never log secrets. A separate plain-text decode journal (see `06-data-model.md`) is not a debug log.

## Portability rules

Windows-only code is confined to: `WasapiAudioBackend`, the credential store implementation, the job-object helper and the installer. Everything else must compile and run on macOS and Linux.
