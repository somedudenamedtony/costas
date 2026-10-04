# 04 · Domain logic

All of this lives in `Ft8Client.Core` and is unit tested without I/O.

## 1. Message parsing

`jt9` returns each decode as text. Parse the text into a typed message.

| Kind | Pattern (tokens) | Example |
| --- | --- | --- |
| `Cq` | `CQ [modifier] CALL [GRID4]` | `CQ K1ABC FN42`, `CQ DX VK3BMT QF22`, `CQ POTA KG5OWB EM12`, `CQ 123 K1ABC FN42` |
| `GridReply` | `TO FROM GRID4` | `K1ABC W7LIT DN40` |
| `Report` | `TO FROM ±NN` | `W7LIT K1ABC -14` |
| `RogerReport` | `TO FROM R±NN` | `K1ABC W7LIT R-08` |
| `Roger` | `TO FROM RRR` | |
| `RogerBye` | `TO FROM RR73` | |
| `Bye` | `TO FROM 73` | |
| `Bare` | `TO FROM` | Nonstandard-call messages with no third field |
| `DxpeditionMulti` | `CALL1 RR73; CALL2 <FOX> ±NN` | Treat as RogerBye to CALL1 and Report to CALL2 from FOX |
| `FreeText` | anything else, up to 13 characters | `TNX BOB 73 GL` |

Rules:

- A CQ modifier is 1 to 4 letters or 1 to 3 digits (`DX`, `NA`, `EU`, `POTA`, `SOTA`, `TEST`, `145`). Keep it; it is shown and affects callability ("CQ NA" from Brazil is callable by a North American station; "CQ EU" is not, for W7LIT).
- Callsign tokens may be wrapped in angle brackets (`<PJ4/K1ABC>`) meaning a hashed call that was resolved, or `<...>` meaning unresolved. Strip the brackets, keep a flag. An unresolved sender cannot be called or tracked.
- Suffixes `/P`, `/R`, `/M`, `/QRP` and prefixes like `PJ4/` are part of the call for display and logging. For country lookup use the country-file rules (prefix wins).
- `RR73` in the grid position is a sign-off, never the grid RR73. `R` followed by a sign and digits is a roger-report.
- Reports range from -30 to +49. Format with sign and two digits.
- Trailing markers from the decoder (`?` for low confidence, `a1` to `a7` for a-priori decodes) are parsed off the end and kept as flags. Low-confidence decodes are shown in Raw decodes but never advance a contact or create a station.

Callsign validity: 3 to 11 characters, letters, digits and `/`, at least one digit and one letter, not a grid, not `RR73`, `RRR`, `73`.

## 2. Country, grid, distance

- **Country file**: load `cty.dat` (AD1C format, https://www.country-files.com/). Lookup order: exact-call entries (prefixed `=`), then longest matching prefix. Each entity gives name, continent, CQ zone, ITU zone, latitude, longitude, primary prefix. The primary prefix is the entity key used throughout the app. Ship a copy; allow in-app update.
- **Grid**: Maidenhead 4 or 6 characters to latitude and longitude of the square's centre.
- **Position of a station**: its grid if known (from its CQ or grid reply, or QRZ lookup), else the entity's coordinates.
- **Distance and bearing**: great-circle from my grid centre. Store kilometres; show miles or km by setting. Bearing is the initial bearing in degrees, rounded.
- **US state and Canadian province**: from QRZ lookup `state` or from a logged contact's `STATE`. If unknown, show the country only.

## 3. Station tracker

One `Station` per callsign per band and mode. Updated after each decoded slot.

| Field | Meaning |
| --- | --- |
| `Call`, `Grid`, `Entity`, `Continent`, `DistanceKm`, `Bearing` | Identity and place |
| `Parity` | Slot parity the station transmits in (even or odd), from the slot it was decoded in |
| `LastHeardUtc`, `LastSnr`, `SnrHistory` (last 6 of its own slots, null where not decoded) | Signal |
| `State` | `CallingCq`, `CallingMe`, `InContact`, `Finishing`, `Quiet` |
| `CqStreak` | Consecutive own-parity slots in which it called CQ |
| `CqModifier` | Latest CQ modifier, if any |
| `Partner`, `PartnerStage` | Who it is working and how far along, when `InContact` |
| `LastMessage` | The latest parsed message |

State transitions, applied when a message from the station is decoded:

| Latest message from station | New state |
| --- | --- |
| `Cq` | `CallingCq`; `CqStreak` + 1 (reset to 1 if the previous own slot was not a CQ) |
| Any message addressed to my call | `CallingMe` |
| `GridReply`, `Report`, `RogerReport` to someone else | `InContact`, `Partner` = addressee; stage "just sent grid", "reports exchanged" |
| `Roger`, `RogerBye`, `Bye` to someone else | `Finishing` |

When one of the station's own-parity slots passes with no decode from it: `SnrHistory` gets a null; after 2 misses in a row the state becomes `Quiet`; after 5 minutes without a decode the station is removed. A station in `Finishing` that is silent for one own slot is treated as free but is not callable until it calls CQ again (it stays in Watching as "Just finished with X").

Trend, for "fading": compare the mean of the last two SNR values with the mean of the two before; a drop of 4 dB or more is "fading", a rise of 4 dB or more is "rising".

## 4. Need tiers

`LogIndex` is built from the local log (which mirrors the QRZ logbook) and answers these questions in memory:

| Tier | Tag | Condition (first that matches) |
| --- | --- | --- |
| 1 | New country | The station's entity has no contact in the log on any band |
| 2 | New band | The entity has contacts, but none on this band |
| 3 | New grid | The station's 4-character grid is known and has no contact in the log |
| 4 | New call | This callsign has no contact on this band and mode |
| 5 | Worked | This callsign has a contact on this band and mode |

Notes:

- Mode grouping: FT8 and FT4 are separate modes for tier 4 and 5. Tiers 1 to 3 ignore mode.
- "Confirmed" is tracked separately for display (band summary, station details) and does not change the tier in v1. A setting "Count only confirmed contacts" switches tiers 1 to 3 to confirmed-only.
- If the log is empty or QRZ was never synced, every station is tier 4 and the UI shows a one-line notice with a link to set up QRZ or import ADIF.
- The tier order is configurable (ranking settings); the default is the table above.
- The index updates immediately when a contact is logged.

## 5. "Hears you" and chance

`HearsMe` holds reception reports of my callsign from PSK Reporter for the current band, keeping the last 60 minutes.

For a station S:

- **Direct report**: the newest report from S's callsign within 15 minutes. Display "Yes, -22".
- **Regional report**: if no direct report, the strongest report within 15 minutes from any receiver in S's entity. Display "Japan does, at -17". For the USA and Canada, use the same state or province if S's is known, else the same 2-character grid field.
- Otherwise "No reports yet".

Chance:

| Chance | Rule |
| --- | --- |
| Good | A direct or regional report of -18 dB or better, and S's SNR at my station is -15 dB or better |
| Fair | A direct or regional report exists, but the Good rule fails |
| Long shot | No direct or regional report in the last 15 minutes |

If PSK Reporter is unavailable, chance is computed from S's SNR alone: Good at -10 or better, Fair at -18 or better, else Long shot, and the Hears you column shows "—".

## 6. Ranking

A station is **callable** when all hold: state is `CallingCq` (or, with "Only stations calling CQ" off, also `Finishing`); it was decoded in its most recent own-parity slot; its call is resolved; its CQ modifier does not exclude me. A continent or country modifier that is not mine excludes me. I may answer `CQ DX` only if the caller is in a different entity from mine. Activity modifiers such as `POTA`, `SOTA` and `TEST` do not exclude.

- **Next in line**: callable, tier 1 to 4, passing the filters. Order by tier (ascending), then chance (Good, Fair, Long shot), then report strength (direct or regional, stronger first, none last), then S's SNR (stronger first), then callsign.
- **Watching**: tier 1 to 4 and not callable. Order by tier, then most recently heard. Reason text from state.
- **Already worked**: tier 5, hidden by default, counted.
- **Calling me**: stations in state `CallingMe` go to "Waiting for you" and are shown in the line with "Calling you" in Doing now, ranked first within their tier.

Filters (ranking settings): prefer stations that hear me (when off, chance is ignored in ordering), only stations calling CQ, hide stations worked on this band, skip long shots.

The ranker is a pure function: `(stations, logIndex, hearsMe, settings, myEntity) -> (line, watching, workedCount)`. Test it with table-driven cases; the sample data in the mockups is one such case.

## 7. Contact engine

A pure state machine. Inputs: operator commands, decoded messages addressed to me, slot ticks. Outputs: the message to transmit in the next slot of my parity (or none), log events, and the step list for the conversation strip.

### Flow A: I answer a CQ (5 steps)

| Step | Who | Message | Advance when |
| --- | --- | --- | --- |
| 1 | Me | `DX ME GRID` (Tx1) | I receive a `Report` from DX |
| 2 | DX | `ME DX ±NN` | — |
| 3 | Me | `DX ME R±NN` (Tx3) | I receive `RogerBye`, `Roger` or `Bye` from DX |
| 4 | DX | `ME DX RR73` | — |
| 5 | Me | `DX ME 73` (Tx5), sent once | Done |

The contact is **logged when step 4 is received**. Step 5 is a courtesy and is sent once.

### Flow B: I call CQ (6 steps)

| Step | Who | Message | Advance when |
| --- | --- | --- | --- |
| 1 | Me | `CQ ME GRID` (Tx6), repeated | A `GridReply` or `Report` to me arrives |
| 2 | DX | `ME DX GRID` | — |
| 3 | Me | `DX ME ±NN` (Tx2) | I receive `RogerReport` from DX |
| 4 | DX | `ME DX R±NN` | — |
| 5 | Me | `DX ME RR73` (Tx4) | Sent |
| 6 | DX | `ME DX 73` | Optional; not waited for |

The contact is **logged when step 5 has been transmitted**. Then CQ resumes, or the next caller in "Waiting for you" is answered if the operator queued one.

### Rules

- My transmit parity is the opposite of the DX station's parity. When calling CQ it is the operator's chosen parity (default even).
- The report I send is the DX station's SNR in the slot that started the contact, clamped to -30..+49, and does not change during the contact.
- Only messages from the DX station addressed to my call advance the contact. Everything else is ignored by the engine.
- If the expected reply does not arrive, repeat my current step. After `RetryLimit` (default 4) repeats with no valid reply, end with outcome `NoReply` and stop transmitting.
- If the DX station repeats an earlier step (it did not copy me), step back and resend the matching reply. Count it in the box's "×N".
- If the DX answers a different station, or calls CQ again before the exchange completes, keep trying up to the retry limit, then end as `NoReply`. (No automatic re-calling beyond the limit.)
- If a `Report` arrives instead of a `GridReply` in flow B (the caller skipped the grid), reply with `DX ME R±NN` and continue as flow A from step 3: the contact is logged when their `RR73` arrives.
- After logging, a repeated `RR73` or `73` from the DX within 2 minutes triggers one more `73` and no second log entry.
- `Resend` repeats the current step in my next slot. `Log now` logs with whatever reports exist. `Abandon` ends immediately with outcome `Abandoned`, releases PTT, logs nothing.
- While a contact is in progress, other stations calling me are added to `WaitingForMe` (maximum 5, oldest first) and are never answered automatically unless the operator pressed "Answer after this contact" for that caller.
- The engine never starts a contact by itself. `CallStation`, `CallCq` and `AnswerCaller` are operator commands.
- Tx watchdog: if no operator input (key, click) has occurred for `WatchdogMinutes` (default 6), stop transmitting and show "Stopped by watchdog".

### Logged fields

Call, grid (if known), band, dial frequency plus my Tx offset, mode, report sent, report received, time on (start of my first transmission in the contact), time off (when logged), my call, my grid, Tx power, name and state and country from QRZ lookup if available, need tier at the time, DXCC entity key.

## 8. Transmit encoding

Port the encode path of `ft8_lib` (MIT, https://github.com/kgoba/ft8_lib: `ft8/message.c`, `encode.c`, `crc.c`, `constants.c`, `text.c`) to C#. Keep its copyright notice.

Pipeline: message text → 77-bit payload → 14-bit CRC → LDPC(174,91) parity → 58 data symbols (3 bits each, Gray coded) with three 7-symbol Costas sync blocks → 79 tone numbers 0..7.

Messages the encoder must support in v1: standard two-call messages with grid, report, R-report, RRR, RR73, 73; CQ with and without modifier; free text up to 13 characters; nonstandard callsign messages (type 4) with hashed calls.

**VERIFY** every message type against the `ft8code` utility that ships with WSJT-X: `ft8code "CQ K1ABC FN42"` prints the 77 message bits and the 79 channel symbols. Build a table of at least 40 messages covering each type (including `/P`, `/R`, compound calls, the report extremes, free text) and assert the C# encoder matches symbol for symbol. If `ft8_lib` and `ft8code` disagree for a type, `ft8code` is correct.

After encoding, decode-check: unpack the 77 bits back to text and compare with what the operator saw. If it differs, refuse to transmit (rule 4 in CLAUDE.md).

### Waveform

- FT8: 79 symbols, 0.16 s each, tone spacing 6.25 Hz, Gaussian frequency shaping with BT = 2.0, continuous phase, with a raised-cosine amplitude ramp over the first and last eighth of a symbol (20 ms). The lowest tone sits at the Tx offset.
- FT4: 103 data and sync symbols plus one ramp symbol each end, 0.048 s per symbol, tone spacing 20.8333 Hz, BT = 1.0. Tone sequence per the QEX paper. **VERIFY** against `ft4code`.
- Render at the output device rate (48 kHz) as 32-bit float mono, peak at the configured digital gain (default -6 dBFS), before the slot starts.
- Changing the Tx offset takes effect at the next transmission, not mid-transmission.

Acceptance: a rendered waveform, resampled to 12 kHz and written as a slot WAV, decodes through `jt9` to the original text with a reported DT between -0.1 and +0.1 s.

## 9. Automatic transmit offset

After each received slot, estimate occupancy across 200 to 2800 Hz from the last 4 slots of my transmit parity that I received (the slots I would transmit in). Take every decode's offset and mark 50 Hz (FT8) or 90 Hz (FT4) as occupied, weighted by recency. Choose the centre of the widest free gap that is at least 100 Hz wide, preferring 1000 to 2000 Hz. Keep the current offset if it is still clear (hysteresis), so it does not jump every slot. Never change it during a contact. Show "Tx offset 1650 Hz · clear" or "· busy" (when no gap was found).

## 10. Clock

`SlotClock` derives slot boundaries from system UTC, driven by a monotonic timer so that system time adjustments do not cause double or missed slots mid-slot. Offset is measured against SNTP at start and hourly, and estimated continuously as the median DT of the decodes in each slot (`jt9` reports DT relative to the nominal start, so a correct clock gives a median near zero). Warn above 0.5 s, block transmit above 2 s (overridable in settings). The app never sets the system clock.
