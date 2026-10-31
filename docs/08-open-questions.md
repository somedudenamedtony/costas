# 08 · Open questions

## Decisions assumed (owner to confirm or change)

| # | Decision | Assumed | If changed |
| --- | --- | --- | --- |
| D1 | Licence | GPLv3, open source | A closed build cannot ship or run `jt9`; it would need `ft8_lib` (weaker decoding) or a new decoder |
| D2 | Stack | C# on .NET 10, Avalonia 12 | The owner raised C# and MAUI; Avalonia was recommended for desktop grids and Linux. WinUI 3 is the alternative if Windows-only is acceptable |
| D3 | Product name | **Decided by the owner (2026-10-04): Costas.** User-Agent `Costas/<version>`, data folder `Costas`, installer `Costas-Setup`; code namespaces stay `Ft8Client`. Data and credentials from earlier builds are migrated on first start | Needed for the installer, the QRZ User-Agent and the PSK Reporter software ID |
| D4 | Decoder packaging | Run `jt9` in place from an installed WSJT-X during development; bundle for release | Bundling needs the dependent DLL set and licence notices |
| D5 | Units | Miles by default, km as a setting | |
| D6 | "Confirmed" in need tiers | Tiers use worked, not confirmed; setting to switch | |
| D7 | Uploading spots to PSK Reporter | Off until the operator turns it on | WSJT-X has it on by default for most users |
| D8 | Split operation | Off in v1 | Enable "Fake it" later if Tx audio purity matters on the owner's radio |
| D9 | Simulation by default | **Decided by the owner (2026-10-04):** no replayed or made-up data unless asked. Simulation only with `--simulate` or the hidden developer mode (seven clicks on the product name in About), which can also use Hamlib's test radio | |
| D10 | Tuning the radio at start-up | Not done: the dial is read from the radio, and only picking a band tunes it. If the radio is outside the selected band, transmit is refused by the band-edge check | Owner to decide whether start-up should tune the radio to the selected band |
| D11 | Updates | **Decided by the owner (2026-10-04):** the repository is public; the app checks GitHub Releases at start and daily (Settings can turn it off; changed from a minute after start by the owner, 2026-10-11), offers a newer build in a bar, and on request downloads it, verifies its SHA-256 and runs the installer, which upgrades in place. Revised by the owner (2026-10-11): the operator still presses Update, but the installer then runs with only its progress window and the app reopens itself when it finishes (revised 2026-10-12 after the owner saw a fully silent update look like nothing was happening and not reopen). Every green build of `main` is published as a release (revised by the owner 2026-10-13: releases are cut on demand by running the CI workflow on `main` by hand; merges only build and test) | |
| D12 | QRZ login | **Revised by the owner (2026-10-04):** the QRZ username and password are asked for again (optional), beside the logbook API key, because QRZ's callsign lookups do not accept the API key. Setup and Settings say why each is needed (`05-integrations.md` section 4). Lookups are on when both are stored. An earlier build of the same day asked for the key alone and deleted any stored login, so operators who used it enter the login again | |
| D13 | ALC and SWR | **Decided by the owner (2026-10-04):** display the meter readings and warnings only (`04-domain-logic.md` section 11). The app never stops, refuses or changes a transmission because of ALC or SWR. **Revised by the owner (2026-10-12):** the ALC now sets the audio level of the next transmission ("I want the ALC to auto adjust"), on by default with a Settings switch to turn it off; a transmission on the air is still never changed, stopped or refused | |
| D15 | Hound mode | **Owner request 2026-10-12** (product review): build Hound mode. **Decided by the owner (2026-10-13):** no switch; calling a recognised Fox works it as a Hound, every other station as before. The operator still starts every contact | A Fox not yet recognised is called the ordinary way |
| D14 | Remote control | **Decided by the owner (2026-10-12):** a page served by the app to a paired phone on the home network (plain HTTP, local addresses only, off by default). A phone takes control with any command; desk input takes it back. While a phone has control, transmit halts after 15 s without a heartbeat and the Tx watchdog is capped at 3 min. Confirmed from the regulation text (LII copy of 47 CFR, read 2026-10-12): 97.109(c) "When a station is being remotely controlled, the control operator must be at the control point"; 97.213(b) requires provisions limiting transmission to 3 minutes if the control link malfunctions | Access from outside the home network would need TLS and a tunnel (Tailscale addresses are already accepted) |
| D15 | Neighbour history | **Decided by the owner (2026-10-12):** store a daily summary of the comparison with stations near me (per UTC day, call, band, mode and continent: counts of each dB difference), plus operator-marked change days, in `neighbour-history.json` for about a year, to answer "did my antenna change help?" (`04-domain-logic.md` section 13, `06-data-model.md`). Display only | |
| D18 | Clock correction | **Decided by the owner (2026-10-20):** "in app clock correction is fine if it works right". Costas corrects its own clock from SNTP (docs/04, section 10), on by default with a setting to turn it off; the PC clock is never changed. Offsets over 60 s are not corrected | Correcting from decode DT, or setting the Windows clock, needs the owner's sign-off |
| D17 | Linux build | **Decided by the owner (2026-10-13):** offer a Linux download next to the Windows one. It starts as a receive-only preview: the owner has no Linux machine to test transmit on, so transmit stays refused until a Linux user has confirmed clean decodes and sane audio levels with a real radio. Each release carries a self-contained `linux-x64` tarball; `wsjtx`, `libhamlib-utils` and `libportaudio2` come from the distribution | Turning transmit on is a later change with the owner's sign-off |
| D16 | POTA spots | Read the public pota.app spot feed by default (setting to turn off). The park is shown under Why and in the details. A contact with a spotted activator is logged with ADIF `SIG=POTA` and `SIG_INFO=<park>`. **Decided by the owner (2026-10-20):** a new park ranks higher: a spotted activator at a park not in the log comes right after the first tier, and is listed in Next in line even when its call is worked on this band (setting, on by default) | Moving an unspotted CQ POTA station up, or any other park rule, needs the owner's sign-off |

## Facts to verify (marked VERIFY in the docs)

| # | Item | Where it matters | How to verify |
| --- | --- | --- | --- |
| V1 | `jt9` stdout line format for FT8 and FT4 | M0 parser | Run on samples, save golden output |
| V2 | Files `jt9.exe` needs beside it | M0, release | Run from a clean folder; note missing-DLL errors |
| V3 | Hashed callsigns across separate `jt9` runs | M0 | Decode two consecutive sample slots containing a nonstandard call |
| V4 | FT4 `-p` value and sample count | M8 | WSJT-X source and a sample FT4 WAV |
| V5 | `rigctld` reply shapes | M4 | Dummy rig, `rigctld -m 1` |
| V6 | QRZ FETCH response encoding and confirmation fields | M2 | One real fetch with the owner's key; redact and save |
| V7 | MQTT topic form for callsigns containing `/` | M6 | mqtt.pskreporter.info page or a live subscription |
| V8 | PSK Reporter query attribute names and published limit | M6 | A live query for a busy callsign; pskreporter.info developer notes |
| V9 | PSK Reporter IPFIX template layout | M6 | pskdev.html IPFIX section; WSJT-X `Network/PSKReporter.cpp` and its IPFIX helper |
| V10 | `QDateTime` wire format in the UDP protocol | M7 | Qt documentation; compare with a datagram captured from WSJT-X |
| V11 | Encoder coverage of nonstandard-call messages in `ft8_lib` | M4 | `ft8code` vectors |
| V12 | Default dial frequencies for FT8 and FT4 | M3 | WSJT-X frequency list (Settings, Frequencies) |
| V13 | Stable download URL for `cty.dat` | M7 | country-files.com |
| V14 | Current stable versions of Avalonia, NAudio, MQTTnet and their .NET 10 support | M0 | NuGet |
| V15 | Whether `rigctld`'s `t` (get_ptt) reads the radio's own transmit state or echoes the PTT Hamlib last set. Setup's transmit test reports it as "the radio reported transmit"; for RTS or DTR PTT Hamlib may only report the serial line it drives | Setup transmit test | Send the test tone on the owner's radio with CAT, then with RTS PTT and the radio unplugged from the PTT line; note what `t` returns. The dummy rig (`-m 1`) answers `PTT: 1` after `T 1` (checked in `RigTests`) |
| V18 | How many reports per minute the all-band sender-square subscriptions `pskr/filter/v2/+/<mode>/+/+/<square>/#` deliver in the evening (USA, FT8). Above 6,000 a minute the client falls back to the current band | Band openings in the band menu | Owner report 2026-10-20: the old country-wide subscription usually said "Too busy to follow". The client now samples other bands in turn after fallback (30 s or 1,000 reports each) and subscribes to squares around the current Settings grid without waiting for or remembering my country. Four-character topic grids, with full locators in the payload, confirmed 2026-10-20 from [M0LTE’s feed consumer](https://github.com/M0LTE/pskrmqtt2db/blob/4df4a930b929d0421faf4c34b6da4ef912ae992c/pskrmqtt2db/Services/PskrFeedService.cs). Still to check on the air: busy-hour rates with these narrower subscriptions and whether sampled estimates track quiet-hour all-band counts |
| V19 | Hound and SuperFox, from WSJT-X source (git `b4f9a43`, 2025-02-04, `widgets/mainwindow.cpp`, `lib/decoder.f90`, `lib/77bit/packjt77.f90`, `lib/superfox/`). A Hound sends only Tx1 and Tx3, never first (even) slot; Tx1 offsets below 999 Hz are moved to a random 1000 to 2999 Hz; the Fox's report triggers `hound_reply`: Tx3 with the report's SNR, Tx frequency set to the Fox's (not in SuperFox mode), and an 11-slot timeout; RR73 to me stops Tx and logs; calling Tx1 stops after 120 s. Without "Rx All" a Hound decodes only below 1000 Hz. The multi-answer message carries a 10-bit Fox hash, which `unpack77` shows as the DX call when it matches `hiscall` (`-x`). SuperFox: decoded only when `params%b_superfox` is set in shared memory (`decoder.f90` calls `sfrx_sub`); the `jt9` command line has no option for it, and the stand-alone `sfrx` is built but not installed. So SuperHound needs the persistent, shared-memory `jt9`; its decodes come out as ordinary lines (`HOUND FOX -12`, `HOUND FOX RR73`, `$VERIFY$ FOX 123456`), so the Hound flow will apply unchanged, except that the R-report stays on my own frequency. Not checked on the air | Source read 2026-10-12 |
| V17 | How many reports per minute the neighbour sender-square subscriptions `pskr/filter/v2/<band>/<mode>/+/+/<square>/#` deliver on a busy band (USA, 20 m FT8). The client stops following above 6,000 a minute | Neighbour comparison | Subscribe for 10 minutes in the evening and count; tried from the cloud build container, which cannot reach ports 1884 or 1886. Subscriptions now use squares around the configured grid (V18) |
| V20 | How LoTW reports FT4 in `lotwreport.adi`. The ARRL page documents no SUBMODE; the parser assumes `MODE=MFSK` with `APP_LoTW_MODE=FT4` (also accepts `SUBMODE`) | LoTW confirmations matching FT4 contacts | Download the owner's report after an FT4 contact is confirmed and read the record |
| V21 | How many reports per minute the hunt subscriptions deliver (`pskr/filter/v2/+/+/<CALL>/#` per call, `pskr/filter/v2/+/+/+/+/+/+/<dxcc>/<my dxcc>/#` per country). The client stops following hunt targets above 1,200 a minute | Hunt alerts on other bands | Hunt a common country (Japan) for an evening and count. Not reachable from the build container |
| V16 | What Windows reports when Setup's port check opens a COM port that another program holds. .NET documents `UnauthorizedAccessException` for it, and Setup says "in use by another program" | Setup radio check | On Windows, open the radio's port in WSJT-X or Flrig, then press Test connection; note the exception and its text. On Linux .NET wraps every open failure in `UnauthorizedAccessException` (captured: missing `/dev/ttyX` gives inner "Unknown error 203"), so Linux shows the raw message instead |

Confirmed from primary sources while writing this package: the `jt9` option table and `<DecodeFinished>` format; the WSJT-X UDP message types and fields; the QRZ Logbook API actions, parameters, responses and User-Agent rule; the QRZ XML login and lookup flow; the PSK Reporter upload rules (host, port, timing); the MQTT broker, ports, topic structure and payload fields; and that the PSK Reporter query endpoint answers with `<receptionReports>` XML.

## Findings (VERIFY items resolved)

| # | Finding | Evidence |
| --- | --- | --- |
| LoTW report | `https://lotw.arrl.org/lotwuser/lotwreport.adi` takes `login`, `password`, `qso_query=1`, `qso_qsl=yes`, `qso_qslsince`, `qso_qsldetail=yes`, `qso_withown=yes`, `qso_owncall`; the header carries `APP_LoTW_LASTQSL`, which is the next `qso_qslsince`; a bad login returns an HTML page with no `<eoh>` | ARRL "Logbook of the World ADIF download" page, read 2026-10-13 |
| TQSL command line | `tqsl -x -d -u -a compliant [-l location] file` signs and uploads in batch mode without dialogs; exit 0 ok, 1 cancelled, 2 rejected, 3 unexpected response, 4 TQSL error, 5 TQSLlib error, 6 cannot open input, 7 cannot open output, 8 all duplicates, 9 some duplicates, 10 command syntax, 11 connection error | TQSL command-line help (lotw.arrl.org TQSL documentation), read 2026-10-13 |
| LoTW user list | `https://lotw.arrl.org/lotw-user-activity.csv` is `CALL,YYYY-MM-DD,HH:MM:SS` per line, no header, about 6.3 MB, no login | Downloaded 2026-10-13 |
| V1 | Confirmed. `jt9` prints `HHMMSS SNR DT FREQ ~  MESSAGE` padded to 37 characters, then markers. FT4 uses `+`. Markers seen: `a1`..`a7` and `?`, in the order `? a3` when both apply. Trailer `<DecodeFinished>   0  22        0` (second integer is the decode count). | Output of WSJT-X 2.7.0-rc3 `jt9` (Ubuntu 24.04 package) on the samples; saved as `samples/**/*.golden.txt` |
| V2 | Linux: `jt9` needs only shared libraries from the distribution (FFTW, gfortran). It writes `decoded.txt`, `jt9_wisdom.dat` and `timer.out` to the data/temp path, so each run gets its own scratch folder. Windows DLL set still to be checked from a clean folder on Windows. | `ls` after a run; `ldd /usr/bin/jt9` |
| V3 | Confirmed: hashed callsigns do **not** resolve across separate `jt9` runs. They resolve within one run (two WAVs passed together), and `-x` with the full DX call does not help. The decode text carries only `<...>`, not the hash value, so our own table cannot substitute. Resolved since 2026-10-13 by keeping `jt9` running in shared-memory mode (`Jt9PersistentDecoder`): the second slot decodes as `<PJ4/K1ABC> W7LIT DN40`, with WSJT-X 2.7.0-rc3 and 3.0.2; with the DX call in `params` it resolves even without the CQ. `-x` in file mode still does not help (3.0.2 too). In file mode (fallback) `<...>` senders stay untracked and uncallable, so the owner decision on letting a `<...>` message advance a contact is no longer needed. | `samples/ft8/260101_000000.wav` + `260101_000015.wav`; `Jt9PersistentDecoderTests` |
| V4 | Partly: WSJT-X writes 72,576 samples (6.048 s) per FT4 slot (sample `000000_000002.wav` has that length). `-p 7` is accepted; with `-L 200 -H 3000` the result is the same as without `-p`. To recheck against WSJT-X 3.0.2 source. | FT4 sample run |
| V13 | `https://www.country-files.com/cty/cty.dat` serves the current file (no versioned path). | HTTP fetch 2026-10-04 |
| V14 | NuGet 2026-10-04: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, NAudio.Wasapi 3.1.0, MQTTnet 5.2.0, Microsoft.Data.Sqlite 10.0.x, Dapper 2.1.89, Serilog 4.4.0, xunit.v3 4.0.1, FluentAssertions 8.11.0 (FluentAssertions 8 is free for non-commercial use only; revisit if that matters). All target .NET 8+ and work on .NET 10. | `dotnet add package` |
| V6 | Confirmed with the owner's key (read-only calls). Replies are `name=value` pairs joined by `&`, keys in no fixed order, not URL-encoded. FETCH puts `ADIF=` last and its text is HTML-entity encoded (`&lt;call:5&gt;`, `&amp;` inside values), so the reply must be split at `ADIF=` before splitting on `&`. Records come in ascending `app_qrzlog_logid`. An empty page is `RESULT=FAIL&COUNT=0` with no reason. A wrong key gives `STATUS=AUTH&RESULT=AUTH&REASON=invalid api key ...`. Confirmation: `app_qrzlog_status` `C`; `qsl_rcvd` and `lotw_qsl_rcvd` also present. FT4 contacts come back as mode `FT4`, not `MFSK`/`FT4`. QRZ can hold two records of the same call, band and mode within 30 minutes under different log ids; the duplicate rule keeps them apart. First real sync: 396 fetched and stored = STATUS `COUNT=396`. Confirmed: 272 locally vs STATUS `CONFIRMED=271`, because one record is confirmed by QSL/LoTW but not QRZ status (the spec's "any of them" rule). | Fixtures in `samples/qrz/` are hand-written in the observed shape and hold no real contact data |
| V5 | Confirmed with Hamlib 4.5.5 `rigctld -m 1`. Plain replies vary by command (`f` gives one line, `m` two, errors `RPRT -n`), so the client uses the extended protocol: prefix each command with `+`; the reply is an echo line, `Key: value` lines (`Frequency`, `Mode`, `Passband`, `PTT`, `Split`, `TX VFO`) and a final `RPRT n`. The dummy rig refuses PTT unless started with `-P RIG`; for CAT PTT the app always passes `-P RIG`. | Captured session against `rigctld -m 1 -P RIG` |
| V11 | Confirmed with `ft8code`/`ft4code` (WSJT-X 2.7.0-rc3): WSJT-X sends a nonstandard call in a standard message only as a hash, and changes some messages silently (`PJ4/K1ABC W7LIT DN40` goes out as `K1ABC W7LIT DN40`; `CQ PJ4/K1ABC FN42` drops the prefix; long free text is truncated; `3DA0XYZ` is not packed as a standard call). The encoder refuses every such message, and the contact engine writes a nonstandard DX call in angle brackets (`<PJ4/K1ABC> W7LIT DN40`), which encodes exactly. Also: WSJT-X sends RR73 as the grid square RR73, right-justifies free text, and does not hash words that are not calls; `ft8_lib` differs on all three and the port follows `ft8code`. | `tests/Ft8Client.Core.Tests/Vectors/*.tsv` (53 FT8 + 53 FT4 vectors) |
| V4 (FT4 encoder) | The FT4 encoder matches `ft4code` for the same vector set, and its rendered waveform (BT 1.0, 0.048 s symbols, ramp symbols as in `ft8_lib`) decodes through `jt9 -5` with DT within 0.1 s. | `WaveformRoundTripTests` |
| V7 | Partly. The broker page (mqtt.pskreporter.info, fetched 2026-10-04) confirms ports 1883/1884 (TLS)/1885/1886, the topic `pskr/filter/v2/{band}/{mode}/{tx_call}/{rx_call}/{tx_grid}/{rx_grid}/{tx_dxcc}/{rx_dxcc}` and the JSON fields `sq f md rp t t_tx sc sl rc rl sa ra b`. It does not say how a call containing `/` appears in the topic. The client subscribes with the call as is (correct if the broker keeps the slash, since `+`/`#` still line up) and checks `sc` in the payload, so a wrong guess loses reports rather than showing other people's. The broker port was unreachable from the build sandbox, so no live subscription was made; the feed test runs against a local MQTTnet broker. **Still to check with a live subscription for a portable call.** | `samples/pskreporter/mqtt-messages.jsonl` (documented shape, example data) |
| V8 | Partly. A live query (`senderCallsign=…&flowStartSeconds=-900&rronly=1`) returns `<receptionReports>` with `<lastSequenceNumber value>` and `<maxFlowStartSeconds value>`; the report attributes used (`receiverCallsign`, `receiverLocator`, `senderCallsign`, `frequency`, `flowStartSeconds`, `mode`, `sNR`) are those in the pskreporter.info developer notes; the reply sampled had no reports to show them. The client queries at most every 5 minutes, only while the live feed is down, sends `lastseqno` after the first reply and `appcontact` when set. | Live query 2026-10-04 (not saved); `samples/pskreporter/query-reply.xml` |
| V9 | Confirmed from WSJT-X `Network/PSKReporter.cpp`: IPFIX version 10, receiver template id `0x50E2` (call, locator, decoder software, antenna, rig; all variable length, enterprise 30351), sender template `0x50E3` (call, 5-byte frequency, 1-byte SNR, mode, locator, 1-byte info source, 4-byte flow start seconds), each set padded to 4 bytes. Templates go in the first three packets and then once an hour; reports every 5 minutes with jitter; one packet under 1,400 bytes. A byte-for-byte test compares the builder with an independent implementation. | `PskReporterTests.Build_OneSpotWithDescriptors_MatchesReferenceByteForByte` |
| V10 | Confirmed from WSJT-X `Network/NetworkMessage.hpp`: `QDateTime` is `qint64` Julian day, `quint32` ms since midnight, `quint8` timespec (1 = UTC; 2 adds a `qint32` offset, 3 a time zone; we send only 1). Field order for types 0, 1, 2, 3, 5, 6 and 12 matches section 7 of `05-integrations.md`; outbound Clear has no window byte; the heartbeat pulse is 15 s. Checked in-app: a listener on 127.0.0.1:2237 received heartbeat, status and decode datagrams that parse with an independent reader. Still to do by hand: GridTracker on Windows. | `WsjtxUdpTests`; live capture 2026-10-04 |
| Rig errors | `rigctld` 4.5.5 says nothing on stderr at its default verbosity when the serial port cannot be opened, and keeps listening; a client's first command then gets the connection closed. With `-vvv` it logs `serial_open: Unable to open <port> - <reason>`. Starting up against a port with no radio, it is not listening until about 10 s after start (it retries the radio first). So Setup's radio check does not parse `rigctld` output: it checks the port is present and opens it itself before starting `rigctld`, and reports a failure after that as "the radio did not answer". | `rigctld -m 3073` against a missing device, a pty with nothing on the far end, and an unreadable file, 2026-10-11 |
| POTA feed | `https://api.pota.app/spot/activator` needs no key and returns a JSON array of spots. `frequency` is kHz as a string (`"14074"`, `"7191.0"`); `spotTime` has no zone and is UTC (matches the response `Date` header); the park name is in `name` (`parkName` was null in all 66 entries); `locationDesc` is like `US-WI`; `invalid` is null. Served from CloudFront with `age` of a few seconds, so polling more often than every minute or two gains nothing; the app polls every 2 minutes. No published rate limit found. Full field list in `05-integrations.md` section 7b. | `curl` through the agent proxy, 2026-10-12 23:37 UTC |

Decode time: single-threaded `jt9` takes 3.4 to 3.7 s on the busy FT8 samples on a 4-core Linux VM, over the 1.2 s target. WSJT-X 2.7 has no `-M`; `Jt9Options.Threads` passes `-M -N n` for WSJT-X 3.x (2.3 to 2.4 s with 3.0.2 built from source, 4 threads). Keeping `jt9` running with WSJT-X's early pass (2026-10-13, 3.0.2, same VM, `DecodeCli --live` against the real clock): 14 to 20 of 21 decodes by 12.8 to 13.4 s into the slot, all of them 1.8 to 1.9 s after the 13.6 s cut-off. The early and later passes are single-threaded, as WSJT-X's are by default. Still to measure on the owner's PC: `DecodeCli --live samples/ft8` with the bundled 3.0.1 `jt9`, and Diagnostics › jt9 runs on the air.

Golden files were captured with WSJT-X 2.7.0-rc3 because it is what Ubuntu packages; recapture with the pinned GA release (3.0.1, the newest Windows build on SourceForge; `05-integrations.md` names 3.0.2, which is not published there) on Windows before release (`scripts/capture-golden.sh`).

Yaesu FT-710 (the owner's radio): Hamlib model 1049, Stable in 4.5.5 and in the 4.6.5 build the Windows fetch script
installs. Checked against a CAT emulator: frequency is `FA`, the data mode `PKTUSB` is `MD0C` (DATA-U), CAT PTT is
`TX1`/`TX0`. Setting the mode with Hamlib's "normal" passband also sent `SH016`, changing the operator's receive
filter, so the app now sets the mode with passband -1 (no change). The FT-710 must have its CAT rate in the radio's
menu match the baud in setup, and use the "Enhanced" USB COM port for CAT. Not yet tried on the real radio.

Radio tuner from Tune: Hamlib's TUNE operation (`G TUNE`) on the FT-710 sends `AC003;` (start tuning), in Hamlib 4.6.5 and
master (`rigs/yaesu/newcat.c`, `newcat_vfo_op`; `ft710.c` lists `RIG_OP_TUNE`); other Yaesu newcat radios send `AC002;`.
A rejected command (`?`, for example with the tuner switched off in the menu) comes back as an error, and Costas then sends
a tune tone instead. Not yet tried on the real radio.

ALC and SWR meters: confirmed with Hamlib 4.5.5. `rigctld -m 1` answers `+l ALC` with `get_level: ALC`, the value on a
line of its own, then `RPRT 0`; an unknown level gives `RPRT -1`. The IC-7300 (model 3073) and FT-710 (model 1049)
backends both list `SWR` and `ALC` as readable. Against the FT-710 CAT emulator, Hamlib reads ALC with `RM4;` and SWR
with `RM6;`; ALC comes back as the raw meter value divided by 64, capped at 1.0 (raw 16 → 0.25, 32 → 0.5, 64 and
above → 1.0), and SWR rises from 1.12 at raw 16 to 2.30 at raw 100. So Hamlib's ALC scale is not the radio's meter
scale, and the 0.5 warning threshold still has to be checked on both real radios (how ALC reads on the radio when the
warning appears).

Transmit power for the log: confirmed with Hamlib 4.5.5 `rigctld -m 1`. `+l RFPOWER` reads the power setting as a
fraction of full power (value on its own line, like ALC); `+\power2mW 0.5 14074000 PKTUSB` answers
`power2mW: 0.5 14074000 PKTUSB`, `Power mW: 50000`, `RPRT 0`. The dummy rig takes 100 W as full power. This is the
setting, not measured output. FT-710: Hamlib 4.6.5's `newcat.c` scales `PC` by its level step of 1/100, so `PC050;`
reads 0.50 and power2mW gives 50 W. Hamlib 4.5.5 divides by 255 instead, so the same radio reads 0.196 and 20 W
(seen against the CAT emulator); only the 4.6.5 build the Windows fetch script installs gets the FT-710 right. Not
yet tried on the real radio or the IC-7300. The app reads power once per transmission on the meter connection and
logs it; a radio that reports nothing (or a value outside 1 to 2000 W) falls back to the power in Settings.

Power output meter (2026-10-27, from the Hamlib 4.6.5 source, `rigs/yaesu/ft710.h` and `newcat.c`): the FT-710
backend lists `RFPOWER_METER_WATTS`, `COMP_METER`, `ID_METER` and `VD_METER` as readable alongside `ALC` and `SWR`.
`l RFPOWER_METER_WATTS` sends `RM5;` and converts with a table borrowed from FLRig (raw 27 = 5 W, 94 = 25 W, 147 =
50 W, 176 = 75 W, 205 = 100 W). SWR uses `RM6;` with a table from G3VPX tests (raw 89 = 2.0, 126 = 3.0). The voltage
table is marked "TBC" in the source, so Costas does not show Vd or Id. Not yet tried on the real radio: compare PO
with the radio's own meter on Tony's first transmission.

Setting the power from the phone (2026-10-25): `+L RFPOWER x` with x = watts / (power2mW of 1.0), then read back. Against
the FT-710 CAT emulator with Hamlib 4.5.5, `L RFPOWER 0.25` sent `PC064;` and 0.5 sent `PC128;` (the same /255 scale),
so 25 W would really go out as 64 W. Costas therefore refuses to read or set power on model 1049 unless
`rigctld --version` says 4.6 or newer. Raw CAT through `+w PC;` was tried as a workaround and is unusable on 4.5.5 (the
reply is lost). `\mW2power 25000 14074000 PKTUSB` answers `Power [0.0..1.0]: 0.250000` (not used). Other radios rely on
Hamlib's own scaling plus the read-back. With Hamlib 4.6.5 built from source, the same emulator reads `PC050;` as 0.5,
power2mW of 1.0 gives 100000 mW, and `L RFPOWER 0.25` sends `PC025;`, so the FT-710 is right there. Not yet tried on the
real FT-710.

Other notes from M2:

- 60 m transmit limits in `frequencies.json` use the ITU WRC-15 range 5351.5 to 5366.5 kHz. US 60 m rules are channelised; the owner should check the limits for his licence before transmitting on 60 m.

## Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| `jt9` interface changes between WSJT-X releases | Decoder breaks after an upgrade | Pin the version; golden-file tests in CI; upgrade on purpose |
| File-mode decode is too slow on busy bands | Late decodes, skipped transmit slots | Multithreaded option; earlier capture cut-off; streaming mode after v1 |
| The MQTT feed is a volunteer service | "Hears you" goes blank | Query fallback; chance computed from SNR alone; clear status |
| QRZ or PSK Reporter rate limiting | Sync or reports stall | Identify the app, back off, cache, keep requests minimal |
| Sequencer edge cases cause bad on-air behaviour | Repeated or mistimed transmissions | Pure state machine with exhaustive tests; simulated partner; owner sign-off on any behaviour change |
| Radio-specific CAT quirks | Setup fails for some radios | Hamlib, port scan, diagnostics bundle, audio-only fallback |
| USB audio device re-enumeration on Windows | Silent receive | Devices by ID, auto-reopen, visible level and status |
| Unsigned installer | SmartScreen warnings | Code-signing certificate in M8 |
| Ranking feels wrong to the operator | The core idea fails | Tier order and filters are settings; the raw decode tab remains; tune thresholds with real use |

## Questions for the owner

1. Which radio and audio interface is the primary test station? (Assumed IC-7300 over USB.)
2. Do you have QRZ XML Logbook Data subscription or higher? Sync and upload need it.
3. Should the app upload your reception spots to PSK Reporter by default?
4. Any preference for the product name?
5. Is the app for your own station first, or for public release from the start? That decides how early the installer, signing and user guide matter.
6. Setup's input meter counts -40 to -10 dBFS (RMS) as a good receive level and calls a sample peak at -1 dBFS clipping. These are a first guess for FT8 band noise on a USB codec; change them if your station sits elsewhere.

### eQSL API verification outstanding

Uploads, inbox downloads and confirmation matching are implemented with synthetic test responses.
Still to verify against a real account: QTH selection and upload/duplicate acknowledgment wording at
`https://www.eqsl.cc/qslcard/ImportADIF.cfm`.

Inbox download, confirmed 2026-10-31 from eQSL's `https://www.eqsl.cc/qslcard/DownloadInBox.txt` (revised 12 Oct 2025)
and live replies with a wrong password: `DownloadInBox.cfm` accepts `UserName`, `Password` and `QTHNickname` as POST
form fields as well as in the URL (both gave the same reply). Success says "Your ADIF log file has been built" and links
the `.adi` and `.txt` files; since 22 Oct 2019 the files live in `/downloadedfiles/`, above `/qslcard/`, so the link is
`../downloadedfiles/…`. Costas only followed links under `/qslcard/`, so every working login read as a failure. An
empty inbox says "You have no log entries". Failures read "Error: No such Username/Password found" (known callsign,
wrong password) or "Error: No such Callsign found" (unknown username); with no credentials, "You are not yet logged in".

Hamlib network models (Hamlib master, `include/hamlib/riglist.h` and `rigs/dummy/*.c`, read 2026-10-13): NET rigctl is
model 2, FLRig 4, TRXManager 5, ACLog 8, SDR# 9. Each takes its server as `host:port` in `-r` (rig_pathname) and
defaults to `127.0.0.1:4532`, `127.0.0.1:12345`, `127.0.0.1:1003`, `127.0.0.1:1100` and `127.0.0.1:4532`. Setup shows
the host:port box for these and prefills the default. Not yet tried against a running flrig.
