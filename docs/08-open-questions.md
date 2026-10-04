# 08 · Open questions

## Decisions assumed (owner to confirm or change)

| # | Decision | Assumed | If changed |
| --- | --- | --- | --- |
| D1 | Licence | GPLv3, open source | A closed build cannot ship or run `jt9`; it would need `ft8_lib` (weaker decoding) or a new decoder |
| D2 | Stack | C# on .NET 10, Avalonia 12 | The owner raised C# and MAUI; Avalonia was recommended for desktop grids and Linux. WinUI 3 is the alternative if Windows-only is acceptable |
| D3 | Product name | Not chosen; working name "FT8 Client" | Needed for the installer, the QRZ User-Agent and the PSK Reporter software ID |
| D4 | Decoder packaging | Run `jt9` in place from an installed WSJT-X during development; bundle for release | Bundling needs the dependent DLL set and licence notices |
| D5 | Units | Miles by default, km as a setting | |
| D6 | "Confirmed" in need tiers | Tiers use worked, not confirmed; setting to switch | |
| D7 | Uploading spots to PSK Reporter | Off until the operator turns it on | WSJT-X has it on by default for most users |
| D8 | Split operation | Off in v1 | Enable "Fake it" later if Tx audio purity matters on the owner's radio |

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

Confirmed from primary sources while writing this package: the `jt9` option table and `<DecodeFinished>` format; the WSJT-X UDP message types and fields; the QRZ Logbook API actions, parameters, responses and User-Agent rule; the QRZ XML login and lookup flow; the PSK Reporter upload rules (host, port, timing); the MQTT broker, ports, topic structure and payload fields; and that the PSK Reporter query endpoint answers with `<receptionReports>` XML.

## Findings (VERIFY items resolved)

| # | Finding | Evidence |
| --- | --- | --- |
| V1 | Confirmed. `jt9` prints `HHMMSS SNR DT FREQ ~  MESSAGE` padded to 37 characters, then markers. FT4 uses `+`. Markers seen: `a1`..`a7` and `?`, in the order `? a3` when both apply. Trailer `<DecodeFinished>   0  22        0` (second integer is the decode count). | Output of WSJT-X 2.7.0-rc3 `jt9` (Ubuntu 24.04 package) on the samples; saved as `samples/**/*.golden.txt` |
| V2 | Linux: `jt9` needs only shared libraries from the distribution (FFTW, gfortran). It writes `decoded.txt`, `jt9_wisdom.dat` and `timer.out` to the data/temp path, so each run gets its own scratch folder. Windows DLL set still to be checked from a clean folder on Windows. | `ls` after a run; `ldd /usr/bin/jt9` |
| V3 | Confirmed: hashed callsigns do **not** resolve across separate `jt9` runs. They resolve within one run (two WAVs passed together), and `-x` with the full DX call does not help. The decode text carries only `<...>`, not the hash value, so our own table cannot substitute. v1 accepts the limitation: `<...>` senders are not tracked or callable. Owner decision needed on whether, in a contact with a nonstandard-call station, a `<...>` message at the DX's offset may advance the contact (an on-air behaviour change). | `samples/ft8/260101_000000.wav` + `260101_000015.wav`, run separately and together |
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

Decode time: single-threaded `jt9` takes 3.4 to 3.7 s on the busy FT8 samples on a 4-core Linux VM, over the 1.2 s target. WSJT-X 2.7 has no `-M`; `Jt9Options.Threads` passes `-M -N n` for WSJT-X 3.x. Measure on the owner's PC with 3.0.2.

Golden files were captured with WSJT-X 2.7.0-rc3 because it is what Ubuntu packages; recapture with the pinned GA release (3.0.2) on Windows before release (`scripts/capture-golden.sh`).

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
