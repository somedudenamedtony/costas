# 09 · Keyboard modes: PSK31, PSK63 and RTTY

Owner decision D18 (2026-10-13): Costas becomes one station for digital modes. PSK31, PSK63 and RTTY join FT8 and FT4 in the Mode menu and use the same Operate screen, so they never look added on. Mockups: `mockups/keyboard-modes/` (approved by the owner 2026-10-13). They were rendered with the app's own styles and controls, and the data in them is made up. The RTTY mockup tags the lower tone as the mark, which is backwards for USB (section 3).

This document is the spec for the keyboard modes. Where it is silent, the existing docs apply unchanged.

## 1. Principles

- **Same screen, same jobs.** Every Operate panel does the job it does for FT8: the contact card runs the contact, Next in line ranks who to work, the waterfall shows the band, and the right column shows who hears whom. There is no separate tab or window for keyboard modes, and no FT8 panel sits empty.
- **The operator sends everything.** Nothing goes out without a key press or a click. Costas only *suggests* the next step. This is the keyboard-mode form of "the operator starts every contact" (`01-product-spec.md`).
- **FT8 is untouched.** The keyboard modes come in alongside the FT8 engine. The FT8 contact engine, decoder and slot timing do not change behaviour, and their tests stay as they are.
- **What you see is what is sent.** Rule 4 applies to typed text: a character the mode cannot send is flagged in the type-ahead line before it goes out. It is never dropped or replaced silently.

## 2. Modes

| Mode | Modulation | Rate | Bandwidth | ADIF |
| --- | --- | --- | --- | --- |
| PSK31 | BPSK, raised-cosine shaped, Varicode | 31.25 Bd | about 60 Hz | `MODE=PSK`, `SUBMODE=PSK31` |
| PSK63 | BPSK, as PSK31 | 62.5 Bd | about 120 Hz | `MODE=PSK`, `SUBMODE=PSK63` |
| RTTY | 2-FSK (AFSK), ITA2 Baudot, 1 start, 5 data, 1.5 stop bits | 45.45 Bd (also 50, 75) | shift 170 Hz (also 200, 425, 850) | `MODE=RTTY` |

The ADIF names were confirmed against ADIF 3.1.6 (adif.org, read 2026-10-13).

**Later, not in scope:** QPSK, PSK125 and up, Olivia, MFSK, CW, FSK keying by the radio, and contest exchanges.

## 3. Radio settings per mode

Picking a mode sets the radio over CAT, the same way FT8 does today (`RigctldRig.SetModeAsync`).

| | PSK31 / PSK63 | RTTY |
| --- | --- | --- |
| Radio mode | `M PKTUSB -1` (USB data), falling back to `USB` as FT8 does | Same. The tones are made by Costas (AFSK) |
| Passband | Left as the radio has it (`-1`), as for FT8 | Same |
| Mark and space | n/a | The mark is the higher RF frequency. In USB, that makes the mark the **higher** audio tone: space = mark − shift. A Reverse switch swaps them for stations on the other sideband |
| Transmit power | Unchanged | Capped at 50 % of the operator's set power by default (`L RFPOWER`), restored when leaving RTTY or closing Costas, unless the operator turned the power since. Changeable in Settings > PSK and RTTY |
| Speech processor | Read with `u COMP` where the radio supports it. If it is on, a warning is shown; Costas does not change it | Same |

Confirmed from Hamlib master (`rigs/yaesu/newcat.c`, `ft710.c`, `ft710.h`, read 2026-10-13):
- On Yaesu newcat radios, `PKTUSB` is mode `C` (DATA-U). `RTTY` is `6` (RTTY-LSB) and `RTTYR` is `9` (RTTY-USB). Costas uses `PKTUSB` for all three modes, because the radio's own RTTY mode needs FSK keying.
- The FT-710 supports `RIG_FUNC_COMP` (get and set) and `RIG_LEVEL_RFPOWER`.
- On the FT-710, Hamlib's default passband for data and RTTY modes is 600 Hz. Costas must therefore never send a passband of `0` (the default); `-1` keeps the radio's own setting.

## 4. Dial frequencies

These are the defaults. They go into `frequencies.json` alongside FT8 and FT4, and the dial box and saved frequencies work as they do today.

| Band | PSK31 / PSK63 | RTTY |
| --- | --- | --- |
| 160 m | 1.807 | 1.807 |
| 80 m | 3.580 | 3.585 |
| 40 m | 7.070 | 7.080 |
| 30 m | 10.135 | 10.130 |
| 20 m | 14.070 | 14.080 |
| 17 m | 18.100 | 18.100 |
| 15 m | 21.070 | 21.080 |
| 12 m | 24.920 | 24.920 |
| 10 m | 28.120 | 28.080 |

Sources:
- The PSK31 column is fldigi's built-in default list (`src/rigcontrol/rigsupport.cxx`, read 2026-10-13). That list uses 7.030 and 7.070 on 40 m; 7.070 is the Region 2 choice.
- The RTTY column is chosen to fall inside the ARRL band plan's RTTY/data segments (arrl.org/band-plan, read 2026-10-13): 3.570–3.600, 7.080–7.125, 10.130–10.140, 14.070–14.095, 18.100–18.105, 21.070–21.110, 24.920–24.925 and 28.070–28.150 MHz.

**VERIFY V22:** check the RTTY activity spots with the owner on the air, and check Region 1 and 3 values before they are offered outside Region 2. 60 m and 6 m are left out until checked.

## 5. Receive

Audio is the same 12 kHz stream from `CapturePump` that feeds `jt9`. It arrives continuously, not cut into slots.

- **Channel finder.** An FFT of the passband finds PSK carriers (a peak about 31 or 62 Hz wide) and RTTY pairs (two peaks one shift apart). Up to 20 channels are decoded at once. A channel is dropped after 60 s without characters.
  - As built (M10): a 4,096-point FFT averaged over about 1.4 s and smoothed over half a signal's width; a peak 6 dB above the band's noise floor is a signal, its carrier the power-weighted centre of the peaks within 0.6 of a signal's width of it (so PSK idle reversals are one signal, and two PSK31 stations 45 Hz apart are two). The floor is the 20th percentile of the band, not the median, so a band more than half full of signals still has one (24 PSK31 stations 105 Hz apart are all found). The SNR column still measures against the median. It looks twice a second.
  - A new channel first reads the last 4 s of audio, so the start of the over that made it visible is not lost.
  - A signal is acted on once two looks in a row find it in the same place. A channel that has been quiet for 1 s (its receiver hearing nothing) is moved onto a signal that turns up near it, keeping its number, and reads the audio since its last text: a reply a few hertz off a CQ is read, where the old receiver, holding the CQ's frequency, was deaf to it. A new channel is not opened within 1.2 signal widths of one already reading (two PSK31 stations 45 Hz apart get a channel each). A channel that keeps printing for 5 s with no signal under it (the edge of a neighbour, a carrier) is dropped unless pinned.
  - Clicking the waterfall in a keyboard mode reads the signal there and keeps reading it through quiet spells (one clicked channel at a time). The signal is the one the finder sees nearest the click (within two bauds for PSK, half a shift and a baud for RTTY); a quiet channel near it is moved onto it.
- **PSK demodulator.**
  - Mix to baseband, apply a matched low-pass filter, then use a Costas loop for carrier phase. Symbol timing comes from the energy at symbol edges.
  - BPSK decides on the phase change, and Varicode is decoded on `00` gaps.
  - AFC tracks drift within ±15 Hz. Squelch uses the symbol-quality metric (the vector-magnitude spread).
  - As built (M10), with the same results and less to tune:
    - No Costas loop: each bit is decided from the phase change between symbols (differential detection), so the carrier phase never has to be held. The mix, decimation to 16 samples a symbol, and a 32-tap Hann matched filter come first.
    - Bit timing balances the matched-filter magnitude of the two halves of the symbol clock; the symbol is taken where the magnitude peaks.
    - AFC follows the squared phase change (the modulation removed), only on clean symbols once locked, and holds once a signal has gone so noise cannot walk it away.
    - Quality is the running average of cos 2Δφ (1 clean, 0 noise); squelch opens at 0.4. Characters are shown at once while quality is 0.8 or more; otherwise they wait 48 symbols and are dropped if quality falls to 0.1, which is how noise read as a signal ends is kept off the screen.
    - Measured on generated signals: PSK31 0 % character errors at −5 dB, 1.4 % at −8 dB and 6 % at −10 dB SNR in 2.5 kHz; PSK63 1.8 % at −5 dB. No characters from 5 minutes of noise.
- **RTTY demodulator.** As built in M11 (`Ft8Client.Modems/Rtty`):
  - Each tone is mixed to baseband, low-pass filtered, decimated to about 16 samples a bit and integrated over one bit (the matched filter for a keyed tone); the envelope is its magnitude.
  - ATC is W7AY's "optimal" form, as in fldigi: each tone's envelope is tracked between its own floor and peak (peak decay about 8 bits, floor rise about 64 bits), and each tone's vote is weighted by its own range, so a tone lost in a fade adds little but noise.
  - Start-bit synchronisation: a mark-to-space edge starts a character, each bit is decided on the middle half of its time, and a character whose start bit is not space or whose stop bit is not mark is dropped. ITA2 with letters/figures shift (fldigi's tables, US figures); CR, NUL and bell are not shown.
  - Unshift-on-space is on by default. AFC tracks within ±30 Hz, only while the signal is clean.
  - Quality is the mark/space contrast |m − s| / (m + s) at the middle of each bit, averaged over about 8 bits; squelch opens at 0.45. Characters are shown at once while quality is 0.8 or more; otherwise they wait 22 bits and are dropped if quality falls to 0.3.
  - Reverse swaps mark and space. Speed (45.45, 50, 75 Bd), shift (170, 200, 425, 850 Hz), Reverse and unshift-on-space are in Settings > PSK and RTTY.
  - The channel finder scores every centre across the band by the weaker of its two tones half a shift either side (each within a twentieth of the shift, at least 7.5 Hz), each at least 3 dB over the noise, and the spectrum must dip between them. Candidates that share a tone (a carrier one shift from a tone, or the mark of one station and the space of the next) are settled so that the most stations, then the strongest pairs of alike tones, are kept. Stations 230 Hz or more apart are each found; 120 to 160 Hz apart only the stronger is. A click within half a shift and a baud of a signal reads that signal.
  - Measured on generated signals (45.45 Bd, 170 Hz): 0 % character errors at 0 dB, about 1 % at −3 dB and 7 % at −6 dB SNR in 2.5 kHz; 4 % with one tone gone at +3 dB; about 10 % with both tones fading independently to −26 dB at +6 dB. No characters from 2 minutes of noise.
- **Signal strength.** The SNR in dB in a 2,500 Hz bandwidth, measured from the FFT, so that the dB column means the same thing as for FT8.
- **Testing.** As of M11 the recordings in `samples/psk31/`, `samples/psk63/` and `samples/rtty/` are synthetic (our modulator with noise), with the text each station sent in a `.expected.txt` file; fldigi recordings are still needed (V25). Every demodulator is tested in CI against WAV recordings in `samples/psk31/`, `samples/psk63/` and `samples/rtty/`, each with an expected-text golden file. Each mode needs at least one clean recording, one weak recording (−10 dB SNR in 2.5 kHz for PSK31) and one drifting recording. Synthetic recordings made by our own modulator are allowed only *in addition* to recordings made with fldigi. Rule 3 still holds: there is no FT8 decoder here. These demodulators are new code, checked against fldigi's output on the same audio.

## 6. Stations from text

Text in a channel is scanned for callsigns using the existing `Callsign` rules. A callsign seen in one of these patterns is that channel's station:

| Pattern | Meaning |
| --- | --- |
| `CQ … DE <call>` or `CQ CQ <call> <call>` | `<call>` is calling CQ |
| `<a> DE <b>` | `<b>` is sending, to `<a>` |
| `<call> <call> K` or `KN` at the end of an over | `<call>` is the sender |

A channel keeps its station until a different sender is seen, or until it has been quiet for 60 s. A channel with no callsign yet appears only as "a signal without a callsign yet, at N Hz" under Next in line.

The `HeardMessage` events from text carry the channel offset and time, and no slot. `StationTracker` treats them like any decode: tiers, worked-before, rarity, LoTW, Hunt list and POTA all apply. `Station.Parity` is empty for these stations.

Ranking:
- A station counts as **callable** while its most recent over contained CQ or QRZ and ended within the last 2 minutes.
- **Doing now** shows the last 60 characters of its text.

## 7. The contact

A keyboard-mode contact shows on the same conversation strip as FT8. These are the steps:

| Called CQ | Answered a CQ |
| --- | --- |
| 1. CQ (mine) | 1. Their CQ |
| 2. Their call | 2. My call (mine) |
| 3. My exchange (mine) | 3. Their exchange |
| 4. Their exchange | 4. My exchange (mine) |
| 5. 73 (mine) | 5. Their 73 |

- **Exchange fields.** RST, name, QTH and grid are found in the partner's text by the patterns `UR 599`, `RST 599`, `NAME <word>`, `OP <word>`, `QTH <words up to .>` and `LOC <grid>`.
- **Capture by click.** The operator can click any word in the conversation and choose Call, Name, QTH, RST or Grid. A clicked value always beats a found one.
- **Moving the strip on.** A step moves to Done when its macro is sent (my steps), or when the partner's over contains what the step needs (their steps). The next step of mine is highlighted, and Enter sends its macro.
- **Logging.** It happens on the step marked Logged, or on Log now, and takes the RST, name and QTH shown in the card. The frequency logged is the dial plus the partner's audio offset, rounded to 1 Hz. This differs from FT8, which logs the dial.
- **Abandon (Esc).** Clears the contact, as for FT8.
- As built (M12): `KeyboardContactEngine` in Core, beside the FT8 `ContactEngine`; the session picks one by mode. A CQ counts as sent as soon as it is queued, so a quick answer is never missed; my other steps are done when their macro has gone out whole. While their step is pending, Enter still offers my next macro, so the operator can move on without waiting. A clicked word is offered from a menu: their name, QTH, report, grid or call.

## 8. Transmit

- **Type-ahead.** Typed text is queued and goes out as the transmitter reaches it. The part already sent is dimmed. Backspace removes characters not yet sent.
- **Macros.** F1 to F12, edited in Settings > Transmitting > Macros. They can contain these fields: `{CALL}` `{NAME}` `{RST}` `{QTH}` `{MYCALL}` `{MYNAME}` `{MYQTH}` `{MYGRID}` `{MYRIG}` `{MYANT}`. A field with no value is shown in red in the type-ahead line, and nothing is sent until it is filled or removed.
- **Defaults** (US-style, editable):

| Key | Macro | Text |
| --- | --- | --- |
| F1 | CQ | `CQ CQ CQ DE {MYCALL} {MYCALL} {MYCALL} {MYGRID} K` |
| F2 | Answer | `{CALL} DE {MYCALL} {MYCALL} K` |
| F3 | Exchange | `{CALL} DE {MYCALL} GE {NAME} UR {RST} {RST} NAME {MYNAME} {MYNAME} QTH {MYQTH} HW? {CALL} DE {MYCALL} KN` |
| F4 | 73 | `TNX {NAME} 73 {CALL} DE {MYCALL} SK` |
| F5 | Rig and antenna | `RIG {MYRIG} ANT {MYANT}` |

- **Return to receive.** Every macro ends with a return to receive, unless more text is waiting. Typed text alone keeps transmitting until Enter on an empty line, or Esc.
- **Where it transmits.**
  - In a contact, on the partner's audio offset. AFC keeps following them.
  - A CQ goes out on the offset last clicked on the waterfall. If nothing has been clicked, it uses the clear spot that the existing `TxOffsetPicker` finds, with the mode's bandwidth.
- **PSK.** Each over opens with 1 s of carrier (phase reversals) and closes with 0.5 s of carrier, then a raised-cosine fade.
- **RTTY.** The idle is LTRS characters. The over opens with a carriage return and line feed (CR LF). After a space that follows figures, the next letter or figure is always sent after its shift (LTRS or FIGS), so receivers with unshift-on-space on and off both print it right: `UR 599 TU` goes out as `UR 599 ` then LTRS `TU`. fldigi does the same job by sending LTRS before every space ("UOS on TX", on by default; `src/rtty/rtty.cxx`, read 2026-10-14); Costas sends the shift only where the receiver's case is in doubt. Approved by the owner, 2026-10-14.
- **Characters.** RTTY sends only ITA2 characters: lower case is sent as capitals as the operator types, since the type-ahead line shows capitals. `@ % & * # _ < > [ ] { }` and accented letters are flagged and not sent. PSK Varicode covers ASCII 0–127; anything else is flagged.
- **Transmit level.** For PSK, Auto ALC aims for zero ALC: the 0.02 threshold becomes 0, because any ALC action spreads a PSK signal (IMD). RTTY uses the FT8 thresholds.
- As built (M12):
  - Keys. While receiving, typing fills the type-ahead line and Enter sends it; the over then stays on the air for more typing. With the line empty, Enter sends the next step's macro. While transmitting, every key goes straight to the transmitter, Backspace takes back what has not gone, and Enter starts a new line (or, on an empty line, returns to receive). F1 to F12 send macros; F1 no longer opens the shortcuts list in these modes. The first Esc stops the over and keeps the contact; the second abandons it.
  - Audio is rendered 0.3 s ahead of the radio, so Backspace can still take back what was just typed.
  - A macro with a field that has no value is refused, and the bottom bar names the fields and where to fill them (Settings, or a word in the conversation). Since M13 the refused macro also appears above the type-ahead line, as it would go out, with each empty field in red; it goes away once every field has a value (the operator then presses the key again) or something else is sent.
  - PSK keeps its own transmit level on each band ("20m PSK"), so its zero-ALC level does not pull the FT8 level down. Any ALC reading at or above 0.01 lowers it.
  - The CQ offset: the waterfall click (which sets a fixed offset), else a clear spot between the stations being read.
  - Audio captured while transmitting is dropped, so Costas never reads its own signal.
  - The AFC switch (M13) sits at the right end of the macro row and is saved as a setting, on by default. Off holds every channel on the frequency it started on.
  - Reverse changes only what Costas reads. It always sends with the mark as the higher tone, so every station reads it.
  - Changing band or mode stops an over on the air.

## 9. Safety

The transmit guards in `Transmitter` apply unchanged:
- PTT is released on any rig or audio error.
- The band-edge check applies.
- Halt Tx and Esc stop transmitting immediately.

The clock check does not apply to keyboard modes, since they have no slots.

There is a watchdog for the keyboard modes:
- Transmit stops after **3 minutes** of continuous transmitting. This matches the remote-control cap in D14.
- It also stops after **20 seconds** of idle with nothing left to send.
- Either stop is shown on the strip as "Stopped by watchdog".
- As built (M12): if an over has still not ended 10 s past the 3-minute limit (the audio device stopped taking samples, so the over cannot play out), Halt drops PTT at once.

## 10. Screen (see `02-ui-spec.md` for the FT8 layout)

**Top bar.** The Mode menu lists FT8, FT4, PSK31, PSK63 and RTTY.

**Contact card.**
- The on-air panel shows "You sent / You received" as RST. The third readout, where FT8 shows the slot countdown, shows "Typed ahead · N characters ready" while receiving, or "Sending · N characters left" while sending, with the progress bar.
- The strip is as in section 7.
- Under the strip, a **conversation box** shows the overs exchanged with this station (time, who, text; my text dimmed). Below it are the type-ahead line, the macro buttons (the next step's button in the accent colour), and one line of hints.
- The Reverse (RTTY) and AFC switches sit at the right end of the macro row.

**Next in line, Repeats and Watching.** These have the same columns as for FT8. Doing now shows the station's latest text in the message font. A line under Next in line counts the signals with no callsign yet.

**Raw decodes tab.** In keyboard modes it shows every channel's text, one rolling line per channel, with the offset.

**Waterfall.** The same control. Each channel has a callsign tag above it ("?" until a callsign is found). The channel in contact is tagged in red. A click starts reading that signal and makes it the CQ offset.

**Right column.**
- *Heard here* and *Your signal* filter to the current band and mode.
- *Band activity* counts signals per three minutes instead of decodes per slot.
- *Tonight* lists contacts in all modes, with the mode on each row.

**Status line.** Shows the mode (for RTTY, with its speed and shift), how many signals are being read, and the station in contact.

**Mini mode and the phone remote.** These show the strip with the macro buttons where the step boxes are. Typing from the phone is a later step.

**Log and Awards.** These gain a Mode filter; "All digital" is the default. Awards count PSK and RTTY under Digital.

As built (M13):
- Mini mode shows the macro keys in a row under the station in place of the step dots, the next step's key in the accent colour; F1 to F12 work while it has the keyboard.
- The phone's contact card shows the macro keys in place of Resend and Call CQ (F1 is the CQ), plus Log now and Abandon in a contact, and the last six lines of the conversation in place of the step messages. A key pressed on the phone goes through the same checks as on the desk, and the phone's 3-minute watchdog cap matches the keyboard over limit.
- The Mode filter offers All digital, All modes, FT8, FT4, PSK and RTTY. All digital is every ADIF mode but SSB, USB, LSB, AM, FM, CW, DIGITALVOICE and PHONE, as ARRL's DXCC Digital award counts them. PSK covers PSK31 and PSK63 (ADIF 3 submodes of PSK) and the older `PSK31` and `PSK63` modes. All digital alone does not show "Clear filters".

## 11. Integrations

- **PSK Reporter spots.** Spots of PSK31, PSK63 and RTTY stations go through the same uploader (section 6 of `05-integrations.md`) with modes `PSK31`, `PSK63` and `RTTY`, and the frequency of the channel. An upload happens once per station per 5 minutes, as for FT8. Confirmed (V23, `08-open-questions.md`): fldigi sends the same names.
- **WSJT-X UDP.** No decodes are sent in keyboard modes, since GridTracker and similar programs understand only WSJT-X decodes. A Status message names the mode, so that they show it. Confirmed from GridTracker source (V24): it shows the mode as given and does not check it.
- As built (M13):
  - Spots: each station named from its text is spotted with the dial plus the channel's audio frequency and the ADIF mode name, at most once per call every 5 minutes (fldigi waits 30 minutes per call and band). The locator is left out, as fldigi does for automatic spots. Off in simulation, as for FT8.
  - UDP: Status, heartbeats, Clear and the logged-contact messages go out as in FT8 (a logged PSK or RTTY contact reaches loggers as Logged ADIF with `MODE` and `SUBMODE`). A Reply from another program is refused in keyboard modes, with a line in the bottom bar, since it cannot name a keyboard station and would otherwise key the radio on another program's guess.
- **QRZ, LoTW, Club Log, eQSL.** Logging uploads use the ADIF modes in section 2 and need no other change.

## 12. Code structure

These steps come in order. Step 1 (M9) changes nothing the operator can see. Steps 2 and 3 are made with the code that first needs them (M12 and M10), so neither interface is designed before it has two users.

1. **Mode definitions.**
   - `Mode` gains `Psk31`, `Psk63` and `Rtty`.
   - `ModeInfo` gains `IsSlotted`, `Adif`, `OccupiedBandwidthHz` and `Name` for each mode. The slot helpers throw for unslotted modes.
   - Every caller of the slot helpers is checked, and either guarded with `IsSlotted` or left FT8-only.
2. **Contact engine interface.**
   - `IContactEngine` is extracted from `ContactEngine`, which keeps its behaviour and tests.
   - `ContactView.MyParity` becomes nullable.
   - As built (M12): not extracted. The two engines share `ContactView` but little else (one plans slots, the other follows text), so the session calls whichever the mode needs.
3. **Heard events.**
   - `HeardMessage` gets a source: a slot decode, or text from a channel.
   - `Decode.SlotStartUtc` stays for FT8, and text events carry their own time.
   - `Station.Parity` becomes nullable.
4. **`Ft8Client.Modems` project** (references Core only):
   - channel finder, PSK and RTTY demodulators and modulators, Varicode and ITA2 tables
   - golden-file tests against recordings
5. **Continuous session.**
   - `KeyboardSession` runs beside `Session` and owns the channel decoders. As built (M12), the keyboard contact engine and the type-ahead (`KeyboardTransmitter`) live in `Session` (`Session.Keyboard.cs`), next to the FT8 engine, so commands, logging and snapshots stay in one place.
   - `Transmitter` gets a streaming audio source next to the slot waveform. Its guards and the keyboard watchdog are shared.
6. **UI.** The Operate changes in section 10, then Raw decodes, Mini, the remote, Log and Awards filters, and the manual pages.
