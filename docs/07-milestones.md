# 07 · Milestones

Build in this order. Each milestone ends with acceptance tests; all must pass before the next starts. "Automated" means it runs in `dotnet test` with no hardware. "Manual" needs the owner at the radio.

## M0 · Skeleton and decode harness

Build: solution and projects per `CLAUDE.md`; CI script (`build`, `test`); `third_party` fetch script; `Jt9Decoder` in file mode with WAV writer and output parser; `DecodeCli`; sample WAV files (the FT8 samples shipped with WSJT-X, plus any recordings the owner supplies) with golden output captured from `jt9`.

Resolve these VERIFY items and record the answers: `jt9` output line format; files needed beside `jt9.exe`; hashed-call behaviour across runs; decode time on the busiest sample.

Accept:
- Automated: `DecodeCli samples/<file>.wav` prints the same decodes as the golden file for every sample.
- Automated: parser unit tests cover normal lines, low-confidence and a-priori markers, FT4 separator, `<DecodeFinished>`, and garbage lines.
- Automated: a `jt9` run that exceeds its timeout is killed and reported, not hung.

## M1 · Core domain

Build in `Core`: message parser; callsign and grid validation; country file loader and lookup; distance and bearing; station tracker; need tiers against an in-memory `LogIndex`; hears-me and chance; ranker; band summary calculator.

Accept:
- Automated: parser table tests for every message kind in `04-domain-logic.md`, including modifiers, hashed calls, compound calls, the DXpedition form and free text.
- Automated: country lookup for at least 30 known calls including exact-match exceptions and prefixes with `/`.
- Automated: distance and bearing from DN40 to five grids within 1% and 1 degree of reference values.
- Automated: tracker scenario tests replaying sequences of slots (CQ streaks, contact with someone else, going quiet, removal).
- Automated: a ranker test that reproduces the sample scenario in `mockups/main-idle.html` row for row.

## M2 · Log and QRZ

Build: SQLite schema and migrations; repositories; ADIF reader and writer; `LogIndex` builder; secret store; QRZ logbook client (STATUS, FETCH with paging, INSERT); sync service; upload queue; QRZ XML lookup with cache; a fake QRZ server for tests.

Resolve VERIFY: FETCH response encoding and confirmation fields (needs the owner's API key once; capture a redacted sample into `samples/qrz/`).

Accept:
- Automated: ADIF round trip; import of a WSJT-X `wsjtx_log.adi`; duplicate rule.
- Automated: full and incremental sync against the fake server, including paging across more than 250 records and an AUTH failure.
- Automated: upload queue survives a restart, backs off on failure, treats a duplicate as uploaded, never sends REPLACE.
- Manual: first sync of the owner's real logbook completes and the totals match QRZ's STATUS.

## M3 · Live receive and the Operate screen (idle)

Build: `IAudioBackend` and WASAPI implementation; resampler; ring buffers; `SlotClock`; `SlotRecorder`; `Session` wiring decode → tracker → ranker; simulation mode (`--simulate`); Avalonia shell with top bar, bottom bar, Operate view in the idle state (band summary, Next in line, Watching, hidden worked, right column with Worked tonight and ranking summary); Raw decodes view; station details flyout; SNTP check and clock indicator.

Accept:
- Automated: slot clock tests with a fake clock (boundaries, system time jump mid-slot).
- Automated: simulation run over the sample folder produces the expected station list after N slots.
- Automated: view-model tests for list stability (no re-rank under the pointer).
- Manual: 24 hours on a live band with zero audio dropouts and spot-check parity against WSJT-X on the same audio.
- Manual: the screen matches `mockups/main-idle.png` in structure and content.

## M4 · Radio and transmit

Build: `rigctld` host and client; serial scan; `SimulatedRig`; FT8 encoder port and waveform; `TxPlayer`; `TxScheduler` with PTT lead and tail; automatic Tx offset; Tune; safety guards (band edge, clock, watchdog, fault → PTT off); band and mode switching through the rig.

Resolve VERIFY: encoder against `ft8code` vectors; `rigctld` reply shapes with the dummy rig.

Accept:
- Automated: at least 40 `ft8code` vectors match symbol for symbol.
- Automated: rendered waveform decodes through `jt9` to the original text with |DT| ≤ 0.1 s.
- Automated: with `SimulatedRig`, PTT asserts 200 ms before audio and releases within 150 ms after; a rig fault mid-transmission releases PTT.
- Automated: guards block transmit out of band and with a 3 s clock offset.
- Manual (dummy load or low power): Tune keys the radio; a manually triggered CQ is decoded by a second receiver or shows up on PSK Reporter; Tx start within 20 ms of schedule by log timestamps.

## M5 · Contacts

Build: contact engine (flows A and B, all rules); top-slot contact view with conversation strip; Resend, Log now, Abandon; Waiting for you; Call CQ; logging on completion; QRZ upload on completion; Worked tonight; QRZ lookup for the current contact; `--simulate-partner`.

Accept:
- Automated: contact engine table tests for every rule in `04-domain-logic.md` section 7 (normal, repeats, step-back, retry limit, skipped grid, late RR73, abandon, watchdog).
- Automated: with `--simulate-partner`, ten contacts in a row complete, log and enqueue uploads with no intervention beyond the initial command; no transmission occurs without an operator command.
- Manual: ten real contacts on the air, each sequenced, logged and uploaded with no manual fix; the screen matches `mockups/main-in-contact.png`.

## M6 · PSK Reporter

Build: MQTT feed client and `spot` storage; query fallback; `HearsMe` wired into ranking (Hears you, Chance); signal card; Reach view with plot label collision handling; spot uploader (IPFIX), off by default.

Resolve VERIFY: topic form for calls with `/`; query attribute names and limit; IPFIX template layout.

Accept:
- Automated: feed client against a local MQTT broker with recorded messages; reconnect and dedupe.
- Automated: IPFIX packets match a byte-for-byte reference built from the documented example or from WSJT-X output captured on the wire.
- Automated: ranking and chance tests with and without reports; graceful behaviour with the feed down.
- Manual: after transmitting, reports appear on the signal card within a few minutes; uploaded spots are visible on pskreporter.info under the owner's call.

## M7 · Setup, settings, log view, interop

Build: six-step first-run setup; Settings; Diagnostics and bundle; Log view with edit, delete, import, export, sync now; station profiles; WSJT-X UDP outbound; decode journal; WAV saving and retention; themes; keyboard shortcuts and accessibility pass.

Accept:
- Automated: UDP datagrams decode correctly with the test reader for every outbound type.
- Automated: settings migration test; secrets never appear in `settings.json`, logs or the diagnostics bundle.
- Manual: a fresh Windows user account goes from install to first decode in under 5 minutes with an IC-7300.
- Manual: GridTracker shows decodes and logged contacts from the app with no configuration beyond its default port.

## M8 · FT4, hardening, release

Build: FT4 receive and transmit (encoder, waveform, slot timing, frequency table); performance pass; installer (MSIX or WiX) with code signing; auto-update; About with licences and credits; user guide.

Resolve VERIFY: FT4 `jt9` parameters; FT4 encoder against `ft4code`.

Accept:
- Automated: FT4 vectors and decode round trip, as for FT8.
- Automated: 24-hour simulation soak with memory under 400 MB and no unhandled exceptions.
- Manual: every quality bar in `01-product-spec.md` is met on Windows 10 and 11.
- Manual: five FT4 contacts on the air.

## After v1

In order of value: band advice, alerts, Hound mode, inbound UDP, LoTW, streaming decode, awards page, then macOS and Linux builds.
