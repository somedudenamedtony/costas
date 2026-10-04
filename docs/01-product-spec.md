# 01 · Product spec

## Purpose

WSJT-X shows the operator a stream of raw decoded messages and leaves the thinking to them: who is that, do I need them, are they free, can they hear me. This app answers those questions itself and shows the result as a ranked list of stations.

It keeps WSJT-X's decoder and on-air behaviour, so a station using it is indistinguishable on the air from one using WSJT-X.

## Users

The everyday HF operator who works FT8 and FT4 from a home station, chases countries, bands and grids, and logs to QRZ. Not aimed at EME, meteor scatter, contesting or DXpedition (Fox) operators in v1.

Reference user: W7LIT, grid DN40, an Icom IC-7300 over USB, Windows 11, a QRZ subscription.

## Goals

1. Decode as well as WSJT-X on the same audio.
2. Install to first decode in under 5 minutes, with no manual.
3. Answer "who should I call next" at a glance, using the operator's own log and live reception reports.
4. Run a contact, log it and upload it to QRZ with one key press and no dialogs.
5. Show the operator where their signal is being heard, live.
6. Never surprise the operator on the air: every transmission is the result of an action they took.

## Principles

- **Stations, not messages.** One row per station, updated in place. The raw decode list exists, behind a tab.
- **The app does the bookkeeping.** Need, availability and "hears you" are computed, not looked up by the operator.
- **Numbers and facts, not prose.** Every value on screen comes straight from data. No generated sentences.
- **One screen to operate.** Nothing needed during a session lives in Settings.
- **The operator starts every contact.** Ranking suggests; it never calls.
- **Optional services stay optional.** Without QRZ the list ranks by signal only. Without PSK Reporter the "hears you" data is blank. The app still works.

## Feature list

### v1 (this build)

| Area | Feature |
| --- | --- |
| Modes | FT8 receive and transmit. FT4 receive and transmit (Milestone 8) |
| Decode | WSJT-X `jt9` decoder, depth 3, a-priori decoding with my call and DX call |
| Stations | Station tracker: one record per callsign per band, activity state, SNR history |
| Need | Need tier per station from the QRZ logbook: new country, new band, new grid, new call, worked |
| Ranking | "Next in line" list ordered by need, then chance; "Watching" list; hidden count of worked stations |
| Chance | Good, Fair or Long shot from PSK Reporter reports and the station's signal |
| Band summary | Idle top slot: stations heard, countries, calling CQ, needed, decodes per slot, heard-from by continent, log totals for the band |
| Contact | Top slot becomes the contact: callsign, need tag, QRZ name and location, reports, five-step conversation strip, Resend, Log now, Abandon |
| Sequencing | Automatic sequencing of the standard exchange, retries, step-back on repeats, RR73 |
| Calling CQ | Call CQ, answer the first caller, queue further callers in "Waiting for you" |
| Transmit | Message encoding, GFSK audio, PTT, automatic clear transmit offset, power setting |
| Safety | Tx watchdog, Tune watchdog, band-edge guard, clock guard, PTT release on any fault |
| Rig | Hamlib via `rigctld`: frequency, mode, PTT, split. PTT by CAT, RTS, DTR or VOX. No-radio audio-only mode |
| Log | Local SQLite log, ADIF import and export, edit and delete, decode journal |
| QRZ logbook | Full sync down (history, confirmations), upload on completion, retry queue, per-contact status |
| QRZ lookup | Name, location, state and grid for the selected or current station, cached |
| PSK Reporter in | Live feed of who hears me (MQTT), query fallback, signal card, reach view |
| PSK Reporter out | Upload my reception spots |
| Worked tonight | Session list with need tag and upload status |
| Raw decodes | Tab with the classic message list, for when it is wanted |
| Interop | WSJT-X UDP protocol, outbound (heartbeat, status, decode, clear, logged, logged ADIF, close) |
| Setup | Six-step first-run setup: station, radio, audio, transmit test, clock check, QRZ |
| App | Station profiles, light and dark theme following the system, diagnostics page, simulation mode |

### v1.x

Hound and SuperHound modes. Alerts for wanted calls or countries. Inbound UDP (reply, halt, highlight). LoTW upload through TQSL and LoTW-user flag. OmniRig, FLRig, TCI. Band advice ("15 m is open to Europe for you"). Awards progress page. Streaming decode (early results before the slot ends).

### Later or not planned

Waterfall (optional panel in the Raw decodes tab). Other modes (WSPR, JT65, Q65, MSK144, FST4, JTTY). Contest exchanges. Fox and SuperFox. Map view. Translations. macOS and Linux builds (design for them; do not build them yet).

## Protocol facts the design relies on

| Item | FT8 | FT4 |
| --- | --- | --- |
| Slot length | 15 s, starting at :00, :15, :30, :45 UTC | 7.5 s |
| Slot parity | Even = :00 and :30. Odd = :15 and :45 | Alternating 7.5 s slots |
| Transmission | 79 symbols, 0.16 s each, 12.64 s, starts 0.5 s into the slot | 105 symbols including ramps, 5.04 s |
| Modulation | 8-tone GFSK, 6.25 Hz spacing, 6.25 baud | 4-tone GFSK, 20.833 Hz spacing |
| Bandwidth | 50 Hz | about 83 Hz |
| Payload | 77 bits + 14-bit CRC, LDPC (174,91) | same |
| Clock tolerance | Decodes reliably within about ±1 s | within ±1 s |
| Audio passband used | 200 to 3000 Hz | same |

Reference: K1JT, K9AN, G4WJS, "The FT4 and FT8 Communication Protocols", QEX July/August 2020, https://physics.princeton.edu/pulsar/k1jt/FT4_FT8_QEX.pdf

Default FT8 dial frequencies (USB), MHz: 1.840, 3.573, 5.357, 7.074, 10.136, 14.074, 18.100, 21.074, 24.915, 28.074, 50.313. FT4: 3.575, 7.0475, 10.140, 14.080, 18.104, 21.140, 24.919, 28.180, 50.318. Keep these in a data file the operator can edit.

## Quality bars

| Measure | Target |
| --- | --- |
| Decode parity | On the sample corpus, at least 99% of the messages WSJT-X decodes at depth 3, and no extra false decodes |
| Decodes on screen | Within 1.5 s of the end of capture for a busy slot |
| Tx start | Within 20 ms of schedule |
| Audio dropouts | Zero in a 24-hour receive soak |
| UI response | Under 50 ms from input to feedback; no stall during decode |
| List stability | A row never moves while the pointer is over it or within 300 ms of a click |
| Cold start | Receiving within 3 s |
| Memory | Under 400 MB after 24 hours |
