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
- Trailing markers from the decoder (`?` for low confidence, `a1` to `a8`, or any `a` and number, for a-priori decodes) are parsed off the end and kept as flags. Low-confidence decodes are shown in Raw decodes but never advance a contact or create a station.

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

When one of the station's own-parity slots passes with no decode from it: `SnrHistory` gets a null; after 2 misses in a row the state becomes `Quiet`; after 5 minutes without a decode the station is removed (or after the operator's "Keep CQ callers listed" time, when that is longer). `LastHeardEndUtc` is the slot start plus the transmit length (13.14 s for FT8); "last heard" ages count from it. A station in `Finishing` that is silent for one own slot is treated as free but is not callable until it calls CQ again (it stays in Watching as "Just finished with X").

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

`ReceptionHistory` also counts distinct receiving callsigns in fixed UTC five-minute intervals, per band and mode,
for twelve completed intervals plus the current partial interval. It uses reception time, accepts late reports,
and deduplicates receivers across feed and fallback reports. The graph labels local times and marks the partial
interval; missing reports count as zero reports, not proof of a closed band. Counts remain available across band
and mode changes in the session and clear when the operator's call or grid changes. Existing stored reports refill
the last hour at start-up. No extra queries are made.

For a station S:

- **Direct report**: the newest report from S's callsign within 15 minutes. Display "Yes, -22".
- **Regional report**: if no direct report, the strongest report within 15 minutes from any receiver in S's entity. Display "Japan does, at -17". For the USA and Canada, use the same state or province if S's is known, else the same 2-character grid field.
- Otherwise "No reports yet".

Chance:

| Chance | Rule |
| --- | --- |
| Good | A direct or regional report of -18 dB or better, and S's SNR at my station is -15 dB or better |
| Fair | A direct or regional report exists, but the Good rule fails |
| Fair | No direct or regional report in the last 15 minutes, and S's SNR at my station is -18 dB or better |
| Long shot | No direct or regional report in the last 15 minutes, and S's SNR is below -18 dB |

No reports mostly means nobody near S uploads to PSK Reporter, not that S cannot hear me, so a strong station without reports is Fair rather than Long shot (owner request 2026-10-12). It is never Good without a report.

If PSK Reporter is unavailable, chance is computed from S's SNR alone: Good at -10 or better, Fair at -18 or better, else Long shot, and the Hears you column shows "—".

## 6. Ranking

A station is **callable** when all hold: state is `CallingCq` (or, with "Only stations calling CQ" off, also `Finishing`); it was decoded in its most recent own-parity slot, or (owner request 2026-10-11) its latest message was a CQ and it was decoded within the operator's "Keep CQ callers listed" time (Ranking setting, default 2 minutes; 0 keeps the old rule); its call is resolved; its CQ modifier does not exclude me. A continent or country modifier that is not mine excludes me. I may answer `CQ DX` only if the caller is in a different entity from mine. Activity modifiers such as `POTA` and `SOTA` do not exclude. Contest modifiers (`TEST`, `CONT`, `RU`, `FD`, `WFD`, `ARRL`, `WW`, `WPX`, `VHF`) make the station not callable (owner decision 2026-10-12): it expects a contest exchange the standard contact cannot send, so it waits in Watching as "Contest (CQ RU), needs an exchange".

A **Fox** (a station heard sending a multi-answer message `CALL1 RR73; CALL2 <FOX> ±NN`) is callable whenever it was decoded in its latest own slot, whatever it is doing, and Doing now reads "DXpedition (Fox/Hound), working Hounds". Calling it starts a Hound contact (flow H).

- **Next in line**: callable, tier 1 to 4, passing the filters. Order by tier (ascending), then calling me first, then stations decoded in their latest own slot before CQ callers kept after a miss, then chance (Good, Fair, Long shot), then report strength (direct or regional, stronger first, none last), then S's SNR (stronger first), then callsign.
- **Watching**: tier 1 to 4 and not callable. Order by tier, then most recently heard. Reason text from state.
- **Already worked**: tier 5, kept out of Next in line and Watching and shown in the Repeats panel (callable first, then not confirmed on this band and mode).
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

### Flow H: Hound (4 steps)

A Fox/Hound DXpedition is worked as a Hound with no mode to switch (owner decision 2026-10-13, replacing the Hound switch of PR #52): calling a station the tracker has seen send a multi-answer message (`RunsFox`) starts flow H; every other contact is flow A or B as before. A Fox not yet recognised (no multi-answer message decoded yet) is called as flow A. A Fox's report answered from Waiting for you also continues as flow H. Behaviour follows WSJT-X (`widgets/mainwindow.cpp`: `hound_reply`, the Hound branch of `readFromStdout`, and the Tx1/Tx3 rules), read from source on 2026-10-12.

| Step | Who | Message | Advance when |
| --- | --- | --- | --- |
| 1 | Me | `FOX ME GRID` (Tx1), repeated, on my own offset of 1000 Hz or more | A `Report` to me from the Fox, alone or in a multi-answer message |
| 2 | Fox | `ME FOX ±NN` or `CALL RR73; ME <FOX> ±NN` | — |
| 3 | Me | `FOX ME R±NN` (Tx3), on the audio frequency the Fox's report came on | `RR73` to me from the Fox |
| 4 | Fox | `ME FOX RR73` or `ME RR73; CALL <FOX> ±NN` | — |

- The Fox always transmits in the first (even) slot; the Hound transmits in the other one.
- Calling a Fox never ends as `AnsweredOther` and is never refused as "in a contact": a Fox is always working other Hounds.
- My report is the Fox's SNR in the slot it answered me (not the SNR when I started calling). A repeated report from the Fox keeps me sending the R-report, with its new SNR and on its new frequency.
- Calling stops after 2 minutes from the first call without an answer (`NoReply`, "KH1X did not answer in 2 minutes. Call it again to keep trying."); the R-report stops 11 slots after the Fox's latest report. The retry limit does not apply.
- The contact is logged when the Fox's `RR73` arrives. Nothing is sent after it: no 73, no free text, and a repeated `RR73` gets no reply.
- In a Hound contact, a multi-answer message addressed to me whose Fox hash did not resolve (`<...>`) counts as from the Fox. `jt9` resolves the hash when the Fox is the DX call (`-x`), which it is during the contact.
- A Fox's report that arrives after calling stopped goes to "Waiting for you"; answering it sends the R-report on the Fox's frequency.
- The quick decode in a Hound contact covers 100 Hz up to 1000 Hz (or the Fox's offset plus 100 Hz if higher), since the Fox answers on any of its streams. The full decode still covers the whole band, so the rest of the screen stays live.
- SuperHound (SuperFox) is not supported yet: the SuperFox signal needs WSJT-X's SuperFox decoder, which `jt9` runs only when started through shared memory (`08-open-questions.md`, V19).

### Rules

- My transmit parity is the opposite of the DX station's parity. When calling CQ it is the operator's chosen parity (default even).
- The report I send is the DX station's SNR in the slot that started the contact, clamped to -30..+49, and does not change during the contact.
- Only messages from the DX station addressed to my call advance the contact. Everything else is ignored by the engine.
- If the expected reply does not arrive, repeat my current step. After `RetryLimit` (default 4) repeats with no valid reply, end with outcome `NoReply` and stop transmitting.
- If the DX station repeats an earlier step (it did not copy me), step back and resend the matching reply. Count it in the box's "×N".
- If the DX answers a different station (sends a `Report`, `RogerReport` or `GridReply` to another call) before it has replied to me, end at once as `AnsweredOther` and stop transmitting; the status says "<DX> answered <call> instead. Stopped calling." (owner request 2026-10-11). A late reply from it is offered with Resume like any stopped contact. Calling a station whose state is `InContact` with another call sends nothing: the contact ends at once the same way, with "<DX> is in a contact with <call>. Not calling." (owner request 2026-10-12). A station in `Finishing` can still be called (tail-ending). A sign-off (`RR73`, `73`) to another call, a CQ, or anything after the DX has replied to me keeps trying up to the retry limit, then ends as `NoReply`. (No automatic re-calling beyond the limit.)
- If a `Report` arrives instead of a `GridReply` in flow B (the caller skipped the grid), reply with `DX ME R±NN` and continue as flow A from step 3: the contact is logged when their `RR73` arrives.
- After logging, a repeated `RR73` or `73` from the DX within 2 minutes triggers one more `73` and no second log entry.
- Message options (owner choice "Plus free text", 2026-10-11): a CQ modifier (`CQ DX ME GRID`); my grid left out of Tx1 (`DX ME`); `DX ME 73` or `DX ME RR73` as my last message in flow A; and optional free text of up to 13 characters sent once in my next slot after that last message, dropped if another contact starts first. Free text that parses as a standard message is refused. After my `RR73`, the DX's `73` is the normal answer and gets no reply; a repeated `RR73` or `RRR` still gets one more. Settings refuses anything that would not encode exactly; the steps and their order never change.
- `Resend` repeats the current step in my next slot. `Log now` logs with whatever reports exist. `Abandon` ends immediately with outcome `Abandoned`, releases PTT, logs nothing.
- While a contact is in progress, other stations calling me are added to `WaitingForMe` (maximum 5, oldest first) and are never answered automatically unless the operator pressed "Answer after this contact" for that caller.
- When a contact ends without being logged (`Abandoned`, `NoReply`, `Watchdog`), the engine remembers it for 5 minutes. Anything its station sends to me in that time, including RR73, RRR or 73, puts it in `WaitingForMe` flagged as a reply after stopping (not counted against the maximum). Nothing is sent until the operator answers it; answering resumes the stopped contact and applies the late messages as if they had arrived in time (a late RR73 logs and sends 73, a late report gets the R-report). Calling or answering that station afresh, or stopping another contact, forgets it.
- The engine never starts a contact by itself. `CallStation`, `CallCq` and `AnswerCaller` are operator commands.
- Tx watchdog: if no operator input (key, click) has occurred for `WatchdogMinutes` (default 6), stop transmitting and show "Stopped by watchdog". Owner decision 2026-10-20: a contact in which the other station has already sent me a message runs to its end first (still bounded by the retry limit, including a free text queued after it); CQ and queued "answer after this contact" callers do not start after it.

### Logged fields

Call, grid (if known), band, dial frequency plus my Tx offset, mode, report sent, report received, time on (start of my first transmission in the contact), time off (when logged), my call, my grid, Tx power (the radio's power setting read through Hamlib during the contact's transmissions, or the power in Settings when the radio cannot report it), name and state and country from QRZ lookup if available, need tier at the time, DXCC entity key, and a comment with the mode and reports in the form other FT8 loggers use (`FT8 Sent: -01 Rcvd: -13`).

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

After each received slot, estimate occupancy across 200 to 2800 Hz from the last 4 slots of my transmit parity that I received (the slots I would transmit in). Take every decode's offset and mark 50 Hz (FT8) or 90 Hz (FT4) as occupied, weighted by recency. Choose the centre of the widest free gap that is at least 100 Hz wide, preferring 1000 to 2000 Hz. When calling a Fox the offset moves to 1000 Hz or above (the fixed offset if it is that high, else the clearest spot there) and holds for the Hound contact; the R-report goes on the Fox's frequency. Afterwards the offset returns to the fixed or automatic choice. Keep the current offset if it is still clear (hysteresis), so it does not jump every slot. Never change it during a contact. Show "Tx offset 1650 Hz · clear" or "· busy" (when no gap was found).

## 10. Clock

`SlotClock` derives slot boundaries from system UTC, driven by a monotonic timer so that system time adjustments do not cause double or missed slots mid-slot. Offset is measured against SNTP at start and hourly, and estimated continuously as the median DT of the decodes in each slot (`jt9` reports DT relative to the nominal start, so a correct clock gives a median near zero). Warn above 0.5 s, block transmit above 2 s (overridable in settings). The app never sets the system clock.

## 11. Transmit meters (ALC and SWR)

While a transmission or test tone is on the air, the app reads the radio's ALC and SWR meters through Hamlib (`l ALC`, `l SWR`) every 0.5 s, starting 0.5 s after the audio starts and stopping 0.5 s before it ends, over a second `rigctld` connection so a meter read never delays a PTT command. A meter that cannot be read is not a fault: it never stops a transmission. `TxMeterCheck` (Core) summarises one transmission by the median of each meter, ignoring SWR below 1 (the radio reports 0 when it measured no forward power):

| Condition | Level | Text |
| --- | --- | --- |
| SWR ≥ 3.0 | Critical | "SWR 3.4 on the last transmission. Check the antenna and tuner before sending again." |
| SWR ≥ 2.0 | Caution | "SWR 2.3 on the last transmission. Check the antenna or tuner." |
| ALC ≥ 0.5 (half of Hamlib's 0 to 1 scale) | Caution | "ALC 70% on the last transmission: the audio is overdriving the radio. Lower Transmit level in Settings or the radio's USB audio level." |

The result stays until a later transmission on the same band reads the meters again, and is cleared on a band change. A transmission whose radio reports neither meter leaves the previous result in place. The readings never stop or refuse a transmission, and never change one on the air (owner decision D13). Automatic transmit level (below) uses the ALC to set the level of the next transmission. **VERIFY** the ALC threshold on the IC-7300 and FT-710: Hamlib scales ALC differently per radio (on the FT-710 it reads 1.0 at a quarter of the radio's own meter).

### Automatic transmit level

On by default (Settings › Transmitting), owner request 2026-10-12. After each transmission or tone whose radio reported ALC, `TxLevelTuner` (Core) moves the audio level for the next one so ALC sits just where it starts to act: full power without ALC compression. Using the median ALC of the transmission on Hamlib's scale:

| Median ALC | Change |
| --- | --- |
| below 0.02 (not acting) | up 0.5 dB, never above the lowest level that made ALC act on this band (less 0.5 dB), the level the operator set plus 6 dB, or 0 dBFS |
| 0.02 to 0.25 | none |
| 0.25 to 0.5 | down 1 dB |
| 0.5 to 0.75 | down 3 dB |
| 0.75 or more | down 6 dB |

The level never goes below the level the operator set less 20 dB, nor below -40 dBFS. A reading outside 0 to 1 is not an ALC reading (SWR read from a rigctld reply out of step is 1 or more) and leaves the level alone.

The lowest level that made ALC act is kept for the session per band, so the level does not swing back and forth; after 10 quiet transmissions held just below it (the radio's power may have been turned up) it is forgotten and the level probes upward again. The level is saved per band (`TxGainByBand` in the station profile); a band with no level of its own uses the level in Settings. Typing a level in Settings sets the current band's level and the base for the 6 dB cap. A radio that reports no ALC, or automatic level turned off, leaves the level alone. When it lowered the level, the ALC warning says so instead of telling the operator to lower it; at the lowest level it keeps telling the operator. Settings schema 2 drops any saved band level more than 20 dB below the operator's: before 0.1.0.336 a reply out of step walked levels down to -40 dBFS, where the radio sends almost nothing. **VERIFY** the 0.02 and 0.25 thresholds on the FT-710 and IC-7300 (where the radio's own ALC meter sits at each).

## 12. Pileup (who else is calling)

`PileupTracker` (Core) watches who calls whom on the current band and mode, from my own decodes only, so the operator can see the competition before calling a station (owner request 2026-10-12). It changes nothing that goes out over the air.

- A **caller** of station T is any station heard sending T its grid or a bare call (`T C DN13`, `T C`). Low-confidence decodes and unresolved calls are ignored. The latest slot in which anyone was heard calling T is T's current pileup, until T has had two chances to answer (four slots).
- A **pick** is T sending a report to a station it was not already working (`C T -12`) right after a slot in which I heard T's callers. A grid or bare call from T is T calling someone, not a pick. A CQ from T ends its current partner.
- A pick is **judged** when I heard the picked caller and at least one other in the slot before. It is a **strongest pick** when no other caller I heard was stronger at my station.
- Callers and picks are kept for 10 minutes and cleared on a band or mode change.

Texts (`PileupText`): "3 stations called JA6VQA at 20:29:30." (or "W7XQ called JA6VQA at 20:29:30."); "Answered the strongest caller you heard in 4 of its last 5 contacts." (from the last 5 judged picks, shown once there are at least 2); "Your offset, 1650 Hz, is clear of them." or "K6ODE is calling 30 Hz from your offset." (a caller within the mode's bandwidth of my transmit offset). These are what I hear, not what T hears: a caller that is strong here may be weak at T.

## 13. Compared with stations near me

`NeighbourComparison` (Core) answers "is it the band or my station?" by comparing how PSK Reporter receivers hear me with how they hear other stations near me, on the same band and mode, over the last 30 minutes (owner request 2026-10-12).

- A **neighbour** is a station whose reported locator is within 300 miles (482.8 km) of my grid, other than me. Without my grid there is nothing to compare.
- For each receiver that heard both me and at least one neighbour: my median report there minus the neighbours' median report there. Comparing at the same receiver cancels out that receiver's antenna, noise and distance.
- The differences are grouped by the receiver's continent (country file). A continent is shown once 3 receivers or more are shared. Each row has the median of my reports, the median of theirs, and the median difference.
- Texts (`NeighbourText`): a difference under 3 dB is "Level"; otherwise "7 dB weaker" or "3 dB stronger". The summary takes the continent with the largest difference: "7 dB weaker than stations near you into Europe." or "Level with stations near you.".
- The neighbours' power is not known: a neighbour running 500 W makes me look weaker. The comparison is shown as data, never used for ranking or transmit.
- **Daily history** (`NeighbourHistory`, owner approval 2026-10-12): every 5 minutes while the comparison has rows, each continent's difference is added to that UTC day's counts for my call, band and mode (stored in `neighbour-history.json`, `06-data-model.md`). A period's figure is the median of every sample in it. Texts (`NeighbourHistoryText`) are exact dB, "level" only at 0: without a mark, the last 7 days against the 7 before ("Into Europe: 4 dB behind neighbours this past week, 7 dB behind the week before."); with a marked change, the days from the mark on against the 14 days before it ("Into Europe: 2 dB behind neighbours since your change on 9 Oct, 7 dB behind in the two weeks before."). Not recorded in simulation or while the comparison is off.

## 14. Band openings from my area

The band menu shows, for each band, whether stations near me are being heard right now and where. Source: PSK Reporter reports of senders in my DXCC entity on every band in my mode (`05-integrations.md` section 7), the same reports the neighbour comparison uses for the current band.

- A report counts when its sender is within 300 miles (482.8 km) of my grid, it is in my mode, it is not my own call, and it is no older than 15 minutes.
- Per band: the number of reports (spots), the distinct senders, and the spots per receiver continent (from the country file).
- Each continent chip is shaded by its spots: none 0, 1 to 9 light, 10 to 49 medium, 50 or more full.
- The activity bar is scaled to the busiest band. A band with no spots says "No spots" while any band has spots; with no spots anywhere, or with the feed or setting off, the menu shows band names only.
- A band whose reports are not being received never says "No spots", which would read as a closed band. Before my DXCC code is known every row says "Needs a spot of you". Reports already held for a band that is no longer followed are not shown.
- **Sampling** (owner request 2026-10-20: "Too busy to follow" left the menu empty most evenings). After the all-band subscription falls back to the current band, the other bands in the menu are sampled one at a time (`BandSampler`): each is subscribed for 30 seconds, or until 1,000 reports of my entity have arrived, and the next starts no sooner than 30 seconds after the last began (at most 2,000 extra reports a minute). After following stops altogether, every band is sampled, the current one included. A sample counts what arrived while it ran, not report times, because senders upload in batches every few minutes. `BandOpenings.BuildSampled` keeps samples that ended in the last 15 minutes and scales their spots and continent counts by 15 minutes over the time listened ("40 spots in 1 minute" reads as 600). These rows show "~600 spots"; the tooltip says the numbers are estimated and how long the band was listened to. A sampled band with nothing near me says "No spots"; one not sampled yet says "Checking soon", with a tooltip explaining the turns. A sample cut short by a mode change is dropped.
- "You need": the receivers' countries with no contact in my log on that band, most spots first: "You need Mongolia", "You need Brazil +2". The tooltip lists them all. An empty log shows nothing here rather than every country.
- Rebuilt at most once a slot while reports arrive; changing band keeps the reports, changing mode clears them.

This shows where the band is open from my area, not who I can hear: a receiver hearing my neighbour is a station listening on that band.

## 15. Rarity (most wanted)

Club Log publishes a most-wanted list of DXCC entities, ranked by how many of its users still need each one (owner request 2026-10-12). `MostWanted` (Core) maps it to country-file entity keys through the ADIF DXCC codes in the bundled `dxcc-adif.csv` (from AD1C's `cty.csv`, the same source and licence as `cty.dat`). `Rarity` turns a rank into a five-step meter:

| Rank | Level | Label |
| --- | --- | --- |
| 1 to 25 | 5 | Very rare |
| 26 to 75 | 4 | Rare |
| 76 to 150 | 3 | Uncommon |
| 151 to 250 | 2 | Fairly common |
| 251 and up | 1 | Common |

The meter is shown only on stations whose need tag is New country, and in the station details. It does not change the ranking. Without the list (never downloaded, Club Log unreachable) nothing is shown.

## 16. Hunt list (alerts)

Owner request 2026-10-12, via the review ideas. The operator lists targets (`HuntTarget`, Core): a country (entity key), a call, a US state, a grid (four characters) or any new country, each on one band or any band. `HuntWatcher` checks candidates and returns alerts. **An alert only notifies: it never changes band, starts a contact or transmits.**

- Candidates come from my own decodes (every kind) and, when the operator allows it, from PSK Reporter on any band (countries and calls only; topics `pskr/filter/v2/+/+/<CALL>/#` and `pskr/filter/v2/+/+/+/+/+/+/<dxcc>/<my dxcc>/#`). A report counts when its receiver is within `nearbyKm` (default 1,000 km) of my grid and is at most 15 minutes old, and only for FT8 and FT4. Above 1,200 hunt reports a minute the client stops following hunt targets (VERIFY V21).
- A target is done when the log has it on its band (any band for an any-band target); with "count only confirmed" on, a country stays wanted until confirmed, and so do a state or grid on an any-band target. Any new country matches an entity not yet worked on any band.
- Each target alerts once per band, then stays quiet on that band for 30 minutes.
- Alerts show in a bar under the top bar (with Go to band, which only changes band and mode), the band menu ("Hunting: …"), mini mode and the phone page. A chime on the default speakers and a taskbar flash are optional (on).

## 17. LoTW confirmations in ranking

`LogIndex.Build` takes a `ConfirmationSource`: `Lotw` (default) counts only `lotw_confirmed`; `Any` also counts QRZ, eQSL and card confirmations. When "count only confirmed" is on, stations that uploaded to LoTW in the last year (`LotwUsers`) sort ahead of others within the same tier, after calling me and heard in the last slot. Owner to confirm both defaults.
