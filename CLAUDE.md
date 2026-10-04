# CLAUDE.md

Project instructions for Claude Code. Read `README.md` and `docs/` before writing code.

## Project

A Windows-first desktop FT8/FT4 client in C#. Station-centric UI, WSJT-X decoder (`jt9`) as a child process, Hamlib `rigctld` for rig control, QRZ logbook for worked-before data, PSK Reporter for "who hears me". Licence GPLv3.

## Stack

- .NET 10, C# latest, nullable enabled, warnings as errors.
- Avalonia 12 with the Fluent theme, MVVM using CommunityToolkit.Mvvm. Compiled bindings on.
- Microsoft.Data.Sqlite with Dapper. No ORM.
- NAudio for Windows audio (WASAPI), behind an `IAudioBackend` interface so other platforms can be added.
- MQTTnet for the PSK Reporter live feed.
- Serilog for logging. xUnit and FluentAssertions for tests.
- Confirm current package versions with `dotnet add package`; do not pin versions from memory.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/Ft8Client.App
dotnet run --project src/Ft8Client.App -- --simulate samples/        # no radio, replays WAV files
dotnet run --project tools/Ft8Client.DecodeCli -- samples/x.wav     # decode one file through jt9
```

## Layout

```
src/Ft8Client.Core/           domain logic, no I/O, no UI
src/Ft8Client.Audio/          capture, playback, resampling, slot clock
src/Ft8Client.Decoding/       jt9 process host, output parser
src/Ft8Client.Rig/            rigctld process host and client, simulated rig
src/Ft8Client.Data/           SQLite, ADIF, settings, secrets
src/Ft8Client.Integrations/   QRZ, PSK Reporter, WSJT-X UDP, time check
src/Ft8Client.App/            Avalonia app: views, view models, composition root
tests/                        one test project per src project
tools/Ft8Client.DecodeCli/    command-line decode harness
samples/                      WAV recordings and expected decodes
third_party/                  jt9 and Hamlib binaries with their licences (not committed; fetched by script)
```

`Core` references nothing but the base class library. `App` is the only project that references Avalonia.

## Rules that are not negotiable

1. **Never key a real radio from tests or on start-up.** Transmit and PTT only happen after an explicit operator action in the UI. Automated tests use the simulated rig and a null audio output.
2. **Simulation is for development and tests, never shown by default.** Replayed recordings and the simulated radio are used only with `--simulate` or the hidden developer mode (owner decision D9). With no radio configured the app shows no made-up data: it receives nothing, the radio indicator says so, and transmit is refused. Do not write code that assumes hardware is present.
3. **Do not write an FT8 decoder.** Decoding goes through `jt9`. The transmit encoder is a port of `ft8_lib`'s encode path and must be checked against `ft8code` test vectors (see `docs/04-domain-logic.md`).
4. **Never alter a message silently.** If a message cannot be encoded exactly as shown to the operator, refuse to send it and say why.
5. **Transmit safety guards are part of the feature**, not a later task: watchdog, band-edge check, PTT release on any rig or audio error, no transmit when the clock is off by more than 2 s.
6. **Audio callbacks do not allocate, lock or log.** Pass samples through a lock-free ring buffer.
7. **Secrets** (QRZ API key, QRZ password) go in the OS credential store, never in settings files, logs or the repository.
8. **No sample callsigns in production code.** Mock data lives under `samples/` and test projects only.
9. **Third-party services are optional.** The app must work with QRZ and PSK Reporter unreachable. Network calls have timeouts, back-off and a visible status.
10. **Respect service limits.** Identify the app in every HTTP request (User-Agent `Ft8Client/<version> (<callsign>)`). Limits are in `docs/05-integrations.md`.

## Conventions

- UTC everywhere in code and storage. Local time only for display.
- Frequencies in hertz as `long`. Audio offsets in hertz as `int`. SNR in dB as `int`. Distances in kilometres internally; convert to miles at display time based on a setting (default miles).
- Callsigns are upper case and compared ordinally. Grids are stored as given, compared on the first four characters, upper case.
- Every external process (`jt9`, `rigctld`) is supervised: start, health check, restart with back-off, kill on app exit.
- One class per file. Public types in `Core` have XML doc comments.
- Each source file starts with the GPLv3 short header.
- Tests are named `Method_Condition_Result`. Domain logic in `Core` needs unit tests before the UI that uses it is built.
- Commit per logical change, imperative subject line, body explains why.

## How to work

- Follow `docs/07-milestones.md` in order. Finish a milestone's acceptance tests before starting the next.
- Items marked **VERIFY** in the docs are facts not yet confirmed from a primary source. Confirm them (read the cited source, or capture real output) before writing code that depends on them, and record what you found in `docs/08-open-questions.md`.
- When the spec and a mockup disagree, the spec wins. When the spec is silent, ask rather than invent on-air behaviour.
- Anything that changes what goes out over the air (message content, timing, automatic retries, who gets called) needs the owner's sign-off before it is changed.
