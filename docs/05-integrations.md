# 05 · Integrations

Each section says what is confirmed from a primary source and what is marked **VERIFY** (recalled, not yet confirmed). Confirm VERIFY items before coding against them and record the result in `08-open-questions.md`.

## 1. Decoder: WSJT-X `jt9`

Source: https://github.com/WSJTX/wsjtx (`lib/jt9.f90`). Baseline version WSJT-X 3.0.2.

### Running it (file mode, v1)

```
jt9 -8 -d 3 -L 200 -H 3000 -c W7LIT -G DN40 -x ZL2RPA -g RF70 -Q 3 \
    -e <bin dir> -a <data dir> -t <temp dir> <YYMMDD_HHMMSS.wav>
```

Options confirmed from the source option table:

| Option | Meaning |
| --- | --- |
| `-8` / `-5` | FT8 mode / FT4 mode |
| `-d DEPTH` | Decoding depth 1 to 3. Use 3 |
| `-L HERTZ`, `-H HERTZ` | Lowest and highest frequency decoded |
| `-f HERTZ` | Receive frequency offset (default 1500). Set to the DX station's offset during a contact |
| `-c CALL`, `-G GRID` | My call and grid (enables a-priori decoding) |
| `-x CALL`, `-g GRID` | DX call and grid, when in a contact |
| `-Q PROGRESS` | QSO progress 0 to 5, for a-priori decoding |
| `-p SECONDS` | T/R period |
| `-e PATH`, `-a PATH`, `-t PATH` | Executable, writable data and temp paths |
| `-M`, `-N THREADS` | Multithreaded FT8 decoder and its thread count (0 to 12) |
| `-m THREADS` | FFT threads |
| `-q` | Quiet: no `<DecodeFinished>` line |
| `-s KEY` | Shared-memory mode (not used in v1) |

Positional arguments are WAV files: `jt9 [OPTIONS] file1 [file2 ...]`.

### Input

- WAV, 12,000 Hz, 16-bit signed, mono. FT8: 180,000 samples (15 s). FT4: 90,000 samples (7.5 s). Zero-pad after the capture cut-off.
- File name `YYMMDD_HHMMSS.wav` in UTC, the slot start. `jt9` takes the decode time from the name.
- Run with the working directory set to a per-run temp folder; `jt9` writes scratch files there.

### Output

- **VERIFY** the line format by running `jt9` on the sample files and saving the output as golden files. Expected for FT8: `HHMMSS SNR DT FREQ ~  MESSAGE [flags]`, for example `025345  -8  0.1 1234 ~  CQ JA1QRS PM95`. FT4 uses `+` in place of `~`. A trailing `?` marks low confidence; `a1` to `a7` mark a-priori decodes.
- The run ends with `<DecodeFinished>` followed by three integers (confirmed: `format('<DecodeFinished>',2i4,i9)`).
- Parse with a tolerant regular expression anchored on the separator character, not fixed columns.

### Details to settle in Milestone 0

- **VERIFY** the correct `-p` value and sample count for FT4.
- **VERIFY** whether hashed callsigns (`<...>`) resolve across separate `jt9` runs. If the hash table lives only in process memory, nonstandard calls heard in an earlier slot will show as `<...>` in file mode. If so, keep our own table of full calls seen and substitute when the 10, 12 or 22-bit hash matches, or accept the limitation until streaming mode.
- **VERIFY** which files `jt9.exe` needs beside it (FFTW, Fortran runtime DLLs). During development, run it in place from an installed WSJT-X (`C:\WSJT\wsjtx\bin\jt9.exe`, path configurable). For release, bundle that set under `third_party/wsjtx/` with the GPLv3 licence text and a link to the exact source.
- Measure decode time on the busiest sample. If it exceeds 1.2 s, try `-M -N 4`.

### Licensing

`jt9` is GPLv3. This app is GPLv3. Credit the WSJT Development Group in About. Their request to derivative works: do not ship features based on WSJT-X release candidates before the matching general-availability release. Track GA releases only.

## 2. Rig control: Hamlib `rigctld`

Hamlib: https://hamlib.github.io/ . Bundle the Windows release under `third_party/hamlib/`.

- Model list: run `rigctl -l` once, parse model number, manufacturer, model name, cache it for the searchable list.
- Start: `rigctld -m <model> -r <port> -s <baud> -t 4532` plus, for a separate PTT line, `-p <port> -P RTS|DTR`. Bind to 127.0.0.1 only. Pick a free port if 4532 is taken.
- Client: TCP, line based. Send a command and newline; read the reply.

| Purpose | Send | Reply |
| --- | --- | --- |
| Get frequency | `f` | `14074000` |
| Set frequency | `F 14074000` | `RPRT 0` |
| Get mode | `m` | mode line, passband line |
| Set mode | `M PKTUSB 0` | `RPRT 0` (fall back to `USB` if the radio rejects `PKTUSB`) |
| Get PTT | `t` | `0` or `1` |
| Set PTT | `T 1` / `T 0` | `RPRT 0` |
| Get split | `s` | split line, Tx VFO line |
| Set split | `S 1 VFOB` | `RPRT 0` |
| Set split Tx frequency | `I 14075000` | `RPRT 0` |

A reply of `RPRT -N` (negative) is an error. **VERIFY** exact reply shapes against the bundled version with `rigctld -m 1` (the dummy rig), which is also used in integration tests.

- Poll frequency and mode every 1 s when idle; do not poll during a transmission.
- Port auto-scan (setup step 2): for each serial port and the model's common baud rates, start `rigctld`, send `f`, accept the first that returns a plausible frequency. Time out at 2 s per attempt.
- VOX / audio-only mode: no `rigctld`; band is chosen by the operator; PTT calls are no-ops.
- Split: default off in v1 (transmit offset is used as is). A setting enables "Fake it" (shift the dial during Tx to keep Tx audio between 1500 and 2000 Hz) once basic operation is proven.

## 3. QRZ Logbook API

Source: https://www.qrz.com/docs/logbook/QRZLogbookAPI.html (confirmed).

- Endpoint: `POST https://logbook.qrz.com/api`, form-encoded name=value pairs in and out.
- Every request has `KEY` (the per-logbook API key) and `ACTION`. Unknown parameters are rejected.
- User-Agent is required, at most 128 characters: `Costas/<version> (<callsign>)`. Generic agents may be rate limited.
- INSERT, DELETE, STATUS and FETCH are marked subscription-required by QRZ.
- A logbook serves exactly one callsign; `W7LIT` and `W7LIT/P` are different logbooks. Store the key per station profile.

| Action | Request | Response |
| --- | --- | --- |
| `STATUS` | — | `RESULT=OK`, `DATA=` name=value pairs (total QSOs, confirmed, DXCC count, start and end dates, book name). Use for the key test |
| `INSERT` | `ADIF=<one record>`; optional `OPTION=REPLACE` | `RESULT=OK&LOGID=n&COUNT=1`, or `RESULT=FAIL&REASON=...`, or `RESULT=REPLACE` |
| `FETCH` | `OPTION=` comma-separated `name:value` pairs: `ALL`, `MODSINCE:YYYY-MM-DD`, `AFTERLOGID:n`, `MAX:n`, `BAND:20m`, `MODE:FT8`, `CALL:x`, `BETWEEN:d1+d2`, `TYPE:ADIF\|LOGIDS`, `STATUS:CONFIRMED\|ALL` | `RESULT=OK`, `COUNT`, `ADIF=` records |
| `DELETE` | `LOGIDS=1,2,3` | `RESULT=OK\|PARTIAL\|FAIL`. Permanent |

`RESULT=AUTH` means the key lacks privileges (no subscription, or wrong key).

### Sync down

1. First sync: `FETCH` with `OPTION=MAX:250,AFTERLOGID:0`; repeat with `AFTERLOGID` = highest `app_qrzlog_logid` received + 1 until fewer than 250 come back. Show a progress count.
2. Later syncs (at start, every 15 minutes, and on demand): `FETCH` with `MODSINCE:<last sync date minus one day>` and the same paging, to pick up edits and new confirmations.
3. Upsert into the local `qso` table keyed by `qrz_logid`.
4. Rebuild `LogIndex` after a sync that changed anything.

**VERIFY** on a real fetch: how the `ADIF` value is encoded in the response (it may be HTML-entity encoded, `&lt;` for `<`), and which fields carry confirmation (`app_qrzlog_status`, `qsl_rcvd`, `lotw_qsl_rcvd`). Treat a contact as confirmed if any of them says so.

### Upload

- On contact completion, write to the local log first, then enqueue an `INSERT`.
- Minimum fields: `call`, `qso_date`, `time_on`, `band`, `mode`, `station_callsign`. Send all fields in `06-data-model.md`.
- Never send `OPTION=REPLACE` automatically; it can overwrite confirmed contacts.
- `RESULT=FAIL` with a duplicate reason counts as uploaded; fetch its logid on the next sync.
- QRZ rejects contacts outside the logbook's configured date range; surface that reason verbatim.
- Queue survives restarts; retry with back-off; at most one request per second.

## 4. QRZ XML lookup

Source: https://www.qrz.com/XML/current_spec.html (confirmed).

- Log in: `GET https://xmldata.qrz.com/xml/current/?username=<u>;password=<p>;agent=Costas<version>` → `<QRZDatabase><Session><Key>…</Key></Session></QRZDatabase>`. Cache the key; do not log in per lookup.
- Look up: `GET https://xmldata.qrz.com/xml/current/?s=<key>;callsign=<call>` → `<Callsign>` with `call`, `fname`, `name`, `addr2` (city), `state`, `country`, `grid`, `lat`, `lon`, `dxcc`, and more.
- A `<Session><Error>` element or a missing `<Key>` means the session expired or the call was not found; re-login once on session errors.
- Full data needs a QRZ XML subscription; without one, fields are limited.
- Look up only the selected station and the current contact, not every decode. Cache results in `callsign_cache` for 30 days. Strip `/P`-style suffixes for the lookup if the full call is not found.
- This uses the QRZ username and password, which are separate from the logbook key. Both optional.

**Why two QRZ credentials.** QRZ runs two separate services with separate sign-ins, and neither accepts the other's:

| Credential | Service | What the app does with it |
| --- | --- | --- |
| Logbook API key (per logbook, from the logbook's settings page) | Logbook API, `logbook.qrz.com/api` | Reads the operator's own logbook (worked and confirmed, for need tiers and log totals) and uploads new contacts. It identifies one logbook, not a QRZ user, and cannot look up other callsigns |
| QRZ username and password | XML callsign data, `xmldata.qrz.com` | Logs in for a session key, then looks up other stations: name, city, state, grid. The contact subline, the station details flyout, the US-state column and the name, QTH and state fields of logged contacts come from here. QRZ only answers lookups for a signed-in user; a QRZ XML subscription gives the full record, a free account gets fewer fields |

Either can be left out. Without the API key there is no worked-before data or upload; without the login, names and locations stay blank and stations are placed by the country file alone. Lookups are on exactly when both a username and a password are stored.

## 5. PSK Reporter: who hears me (live feed)

Source: https://www.mqtt.pskreporter.info/ (confirmed). A best-effort community service run by M0LTE on top of PSK Reporter. Treat as optional.

- Broker `mqtt.pskreporter.info`. Ports: 1883 TCP, 1884 TLS, 1885 WebSockets, 1886 WebSockets with TLS. Use 1884. No account.
- Topic: `pskr/filter/v2/{band}/{mode}/{tx_call}/{rx_call}/{tx_grid}/{rx_grid}/{tx_dxcc}/{rx_dxcc}`. Wildcards `+` (one level) and `#` (rest).
- Subscribe to `pskr/filter/v2/+/+/<MYCALL>/#`.
- Payload JSON: `sq` sequence, `f` frequency Hz, `md` mode, `rp` report dB, `t` epoch seconds, `sc`/`rc` sender and receiver calls, `sl`/`rl` locators, `sa`/`ra` ADIF DXCC codes, `b` band.
- **VERIFY** how a callsign containing `/` appears in the topic.
- Reconnect with back-off. Store each report in `spot` (dedupe on receiver, band, 5-minute bucket; keep the strongest). Prune after 24 hours.

### Fallback and history: query service

Confirmed live: `GET https://retrieve.pskreporter.info/query?senderCallsign=<MYCALL>&flowStartSeconds=-3600&rronly=1&appcontact=<email>` returns XML rooted at `<receptionReports>` with `<lastSequenceNumber>` and `<maxFlowStartSeconds>` and zero or more `<receptionReport>` elements.

- **VERIFY** the `receptionReport` attribute names (expected: `receiverCallsign`, `receiverLocator`, `senderCallsign`, `senderLocator`, `frequency`, `flowStartSeconds`, `mode`, `sNR`, `receiverDXCC`).
- Use it once at start (to fill the last hour) and every 5 minutes only while the live feed is down. Never more often than once per 5 minutes; pass `lastseqno` from the previous answer to get only new reports. **VERIFY** the published limit.

## 6. PSK Reporter: uploading my spots

Source: https://pskreporter.info/pskdev.html (confirmed rules below).

- UDP to `report.pskreporter.info` port 4739, IPFIX (RFC 5101) packets.
- Send at most one packet every five minutes unless a packet fills up. Do not align sends to the clock; add random jitter. Use one UDP source port for the app's lifetime.
- Send the record-format descriptors (templates) with the first three packets after start and then at least hourly.
- Report each callsign at most once per five minutes, ideally once per hour unless something changed (band).
- A spot needs at least the sender callsign; send frequency, SNR, mode, sender locator, information source and time as well. The receiver record carries my callsign, locator and software name and version.

**VERIFY** the exact template layout before coding. Read the IPFIX section of pskdev.html and the WSJT-X reference implementation (`Network/PSKReporter.cpp` in the WSJT-X source, GPLv3, may be ported). Expected: enterprise number 30351; fields senderCallsign (.1), receiverCallsign (.2), senderLocator (.3), receiverLocator (.4), frequency (.5), sNR (.6), iMD (.7), decoderSoftware (.8), antennaInformation (.9), mode (.10), informationSource (.11), plus standard `flowStartSeconds` (150); variable-length strings with a one-byte length prefix; each set padded to 4 bytes.

Spot only decodes with a resolved callsign that are not low confidence. Never spot my own call. Off by default until the operator turns it on in setup step 6 or Settings.

## 7. WSJT-X UDP protocol (outbound)

Source: `Network/NetworkMessage.hpp` in the WSJT-X source (confirmed). Lets GridTracker, JTAlert, N1MM+ and loggers work unchanged.

- UDP to a configurable address, default `127.0.0.1:2237`. Unicast or multicast.
- Every datagram: `quint32` magic `0xadbccbda`, `quint32` schema (send 2), `quint32` message type, then `utf8` Id (a unique name, `Costas`), then the fields.
- Encoding is Qt `QDataStream`, big-endian. `utf8` = `quint32` byte length then bytes; length `0xffffffff` = null. `bool` = one byte. `QTime` = `quint32` milliseconds since midnight. Floating point = 64-bit IEEE double. **VERIFY** `QDateTime` (expected: `qint64` Julian day, `quint32` ms since midnight, `quint8` timespec, 1 = UTC).

| Type | Name | Fields after Id, in order |
| --- | --- | --- |
| 0 | Heartbeat | max schema (quint32) = 3, version (utf8), revision (utf8). Every 15 s |
| 1 | Status | dial frequency Hz (quint64), mode, DX call, report, Tx mode (utf8 each), Tx enabled, transmitting, decoding (bool each), Rx DF, Tx DF (quint32 each), DE call, DE grid, DX grid (utf8 each), Tx watchdog (bool), sub-mode (utf8), fast mode (bool), special operation mode (quint8) = 0, frequency tolerance (quint32), T/R period (quint32), configuration name (utf8), Tx message (utf8). On every change |
| 2 | Decode | new (bool), time (QTime), snr (qint32), delta time s (double), delta frequency Hz (quint32), mode (utf8, `~` for FT8, `+` for FT4), message (utf8), low confidence (bool), off air (bool) |
| 3 | Clear | (none outbound). On band change |
| 5 | QSO Logged | date-time off (QDateTime), DX call, DX grid (utf8), Tx frequency Hz (quint64), mode, report sent, report received, Tx power, comments, name (utf8 each), date-time on (QDateTime), operator call, my call, my grid, exchange sent, exchange received, ADIF propagation mode (utf8 each) |
| 6 | Close | (none). On exit |
| 12 | Logged ADIF | ADIF text (utf8): a header and one record |

Inbound messages (Reply 4, Halt Tx 8, Free Text 9, Highlight Callsign 13, and others) are v1.x. In v1, listen only to keep the socket healthy and ignore content.

Test by decoding our own datagrams with a small reader in the test project and, manually, against GridTracker.

## 8. Country file

`cty.dat` from https://www.country-files.com/ . Ship a copy in the app; "Update country file" in Settings downloads the current one (**VERIFY** the stable download URL), validates that it parses and has more than 300 entities, then swaps it in.

## 9. Time check

SNTP over UDP port 123 to `pool.ntp.org` (configurable). Take 4 samples, use the one with the lowest round-trip delay. Run at start and hourly. Offline is not an error; fall back to the DT estimate.

## 10. Audio devices (Windows)

NAudio, WASAPI shared mode by default. Identify devices by their endpoint ID and show the friendly name. Capture at the device's mix format, convert to mono float, resample to 12 kHz with a windowed-sinc resampler. Never use the default device implicitly: if the configured device is missing, receive stops with a clear status and transmit is disabled. Watch for device removal and arrival and reopen automatically.

## 10. Update check

GitHub Releases for `somedudenamedtony/costas` (public). CI publishes every green build of `main` as release `v<version>` with `Costas-Setup-<version>.exe` and `Costas-Setup-<version>.exe.sha256` attached.

- `GET https://api.github.com/repos/somedudenamedtony/costas/releases/latest` with the app's User-Agent and `Accept: application/vnd.github+json`. Unauthenticated limit is 60 requests an hour per address; the app asks a minute after start and then once a day. 404 (no release yet, or the repository is private) means "no update", not an error.
- The checksum comes from the asset's `digest` field (`sha256:<hex>`) when GitHub provides it, otherwise from the `.sha256` asset. With neither, the app refuses to install the download.
- Draft and pre-release releases are ignored. A release is offered only when its version is greater than the running build's, and not after the operator chose "Skip this version".
- Install: download to the data folder's `tmp/updates`, verify, start the installer, close the app normally (PTT released, child processes stopped). Refused while transmitting or in a contact. The installer upgrades in place.

