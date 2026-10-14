# PSK31, PSK63 and RTTY

!!! note "Preview"
    PSK and RTTY are new and have not yet been tried on the air. Watch your first few contacts closely, and keep
    **Halt Tx** (Esc) in reach.

Pick **PSK31**, **PSK63** or **RTTY** in the **Mode** menu. Costas tunes the radio to the band's PSK frequency
(14.070 MHz on 20 m) or RTTY frequency (14.080 MHz) in the same data mode it uses for FT8, so nothing else needs
changing on the radio. For RTTY that is right too: Costas makes and reads the two tones itself, so the radio's own RTTY
mode is not used.

![PSK31 on the Operate screen](images/psk31-operate.png)

## What you see

These modes have no time slots: stations type whenever they like, and Costas reads every signal in the receive
passband at once (up to 20).

- **Next in line, Repeats and Watching** work as they do for FT8. Costas finds each station's call in its text
  ("CQ CQ DE KP4JRS KP4JRS K", "W7LIT DE EA8TL") and ranks it by what you need. A station is listed as calling CQ
  for two minutes after its CQ, and the list refreshes every few seconds.
- **The waterfall** labels each signal with its station's call, "?" until Costas has read a call, and the station you
  are in contact with in red. Click a signal to read it, even before Costas has noticed it; it stays read while that
  station is quiet. For RTTY, click either of its two tones or between them.
- **Signals without a callsign yet** are counted in a line under Next in line, with their audio frequencies.
- **Band activity** (right column) and the band summary count the stations read every three minutes, as there are no
  slots to count.
- **Raw decodes** shows the text, a line per over or per line typed, with the audio frequency and signal strength. The
  DT column is empty: these modes have no time offset.
- **dB** is measured in a 2,500 Hz bandwidth, the same scale as FT8, so −10 dB means the same in both.

There are no slots, so where FT8 shows the slot countdown the contact card shows how many characters you have typed
ahead, or how many are still to go while sending.

## Making a contact

Nothing is ever sent without a key press or a click from you.

![Answering a PSK31 CQ](images/psk-sending.png)

**Answering a CQ.** Pick the station in **Next in line** and click **Call** (or double-click it). Costas sends your
answer (F2) on the station's own audio frequency and moves the contact strip on, just as for FT8:

1. Their CQ
2. Your answer (F2)
3. Their exchange: Costas reads their report, name, QTH and grid from what they send ("UR 579", "NAME JOSE",
   "QTH SAN JUAN.", "LOC FK68")
4. Your exchange (F3)
5. Their 73: the contact is logged

**Calling CQ.** Press F1 or **Call CQ**. A station that answers with your call starts the contact, and the strip runs
CQ, their call, your exchange (F3), their exchange, then your 73 (F4), which logs it.

**The conversation box** under the strip shows what each of you sent, with your own text dimmed. It keeps its size
and only follows new text while you are at the end, so words and keys do not move as you aim at them. If Costas read
a name or QTH wrong, or missed it, click the word in the conversation and choose what it is: their name, QTH, report,
grid or call. What you pick always wins over what Costas read, and the name and QTH to be logged show under the call.
A contact you abandon stays on the card, marked Abandoned, but stops collecting their text.

**Keys.**

| Key | While receiving | While sending |
| --- | --- | --- |
| Typing | Goes into the line under the conversation | Goes out as you type |
| Enter | Sends what you typed, or the next step's macro (its button is highlighted) | A new line; on an empty line, back to receive once everything has gone |
| Backspace | Edits the line | Takes back what has not gone out yet |
| F1 to F12 | Send that macro | Add that macro to what is going out |
| Esc | Abandons the contact | Stops sending at once; press again to abandon the contact |

These keys work wherever you last clicked on the Operate screen: typing, Space, Backspace and Enter always go to the
typing line, never to a button. A macro goes back to receive when it has gone out, unless you keep typing. Typed text
keeps the transmitter on, sending idle between words, until you press Enter on an empty line. Keys typed while an over
is closing wait in the typing line for your next Enter.

**Characters.** RTTY sends capitals only, so the typing line shows capitals in RTTY. A character the mode cannot send
(in RTTY `@ % & * # _ < > [ ] { }` and accented letters; in PSK anything outside plain ASCII) is named in red under
the typing line before you press Enter. If you press Enter anyway, nothing is sent, the bar says why, and your line is
kept so you can fix it. While sending, such a key is left out and named under the line.

**Resend and the CQ modifier** are FT8 and FT4 controls and are hidden in these modes: send again with the macro keys.

**Logging.** The contact is logged with the report, name and QTH shown on the card, and the frequency of the
station's signal (the dial plus its audio frequency). Reports are RST, such as 599. **Log now** logs it at any time.

## Macros

The macro keys and your details are in **Settings › PSK and RTTY**. The defaults are:

| Key | Macro | Text |
| --- | --- | --- |
| F1 | CQ | `CQ CQ CQ DE {MYCALL} {MYCALL} {MYCALL} {MYGRID} K` |
| F2 | Answer | `{CALL} DE {MYCALL} {MYCALL} K` |
| F3 | Exchange | `{CALL} DE {MYCALL} GE {NAME} UR {RST} {RST} NAME {MYNAME} {MYNAME} QTH {MYQTH} HW? {CALL} DE {MYCALL} KN` |
| F4 | 73 | `TNX {NAME} 73 {CALL} DE {MYCALL} SK` |
| F5 | Rig and antenna | `RIG {MYRIG} ANT {MYANT}` |

Fill in **Your name**, **Your QTH** and your radio and antenna there first. A macro whose field has no value is not
sent: it appears above the typing line as it would go out, with the empty field in red, and the bottom bar says where
to fill it. Click the right word in the conversation (for their name or QTH) or fill in Settings, then press the key
again. Your report to them is 599.

When you save, Settings checks every macro: a name in braces that is not one of the fields above (a typo such as
`{NAM}`) or a character neither mode can send stops the save and names the key. A character only RTTY cannot send, such
as `@`, is a warning: press Save again to keep it, and that key will be refused in RTTY. **Remove** empties a key (the
last added key goes altogether). If every key is empty, the default macros come back; Settings says so first.

## Away from the desk

- **Mini mode** shows the macro keys under the station instead of the step dots, with the next one in blue. F1 to F12
  work in the strip too.
- **The phone** shows the macro keys on the Operate tab instead of Resend and Call CQ (F1 is your CQ), with the last
  few lines of the conversation. You cannot type from the phone yet.
- **PSK Reporter.** With spot uploading on, stations Costas reads are reported as PSK31, PSK63 or RTTY, at the
  frequency of their signal.
- **GridTracker and loggers** see the mode in the status Costas sends, and logged contacts as usual. Costas sends them
  no PSK or RTTY text, so calling a station from GridTracker works only in FT8 and FT4.

## Safety

- Costas will not send outside the band's limits, and it drops PTT on any radio or audio error.
- A single over stops after 3 minutes, and an over with nothing to send stops after 20 seconds. The strip then says
  "Stopped by watchdog".
- RTTY keeps the transmitter at full output for the whole over. When you pick RTTY, Costas turns the radio's power down
  to half of what you set, and puts it back when you leave RTTY or close Costas (unless you turned the power yourself
  meanwhile). Change the share under **RTTY power** in Settings; 100 % leaves the power alone.
- For PSK, automatic transmit level aims for no ALC at all, since any ALC action makes a PSK signal wider. It keeps a
  separate level for PSK on each band, so your FT8 level is not affected.
- If the radio's speech processor is on, a warning appears under the macro keys. It distorts PSK and RTTY; turn it
  off on the radio. Costas does not change it.

## Tips

- Costas reads a signal down to about −10 dB (PSK31). Below that it shows nothing rather than guessing, and it never
  shows text from plain noise.
- PSK63 is twice as fast and needs a few dB more signal.
- Keep the radio's receive filter at least 2.5 kHz wide so every signal in the waterfall can be read.
- **AFC**, beside the macro keys, lets each channel follow a signal that drifts. It is on by default. On a crowded band,
  if the text of one station turns into another's, turn it off so each channel stays where it started.

## RTTY settings

![RTTY on the Operate screen](images/rtty-operate.png)

RTTY has a few settings, in **Settings › PSK and RTTY**. The status line shows the ones in use, for
example "Reading RTTY 45.45 Bd, 170 Hz shift". **Reverse** is also beside the macro keys. It changes only how
Costas reads; what you send is always the right way round.

| Setting | Default | What it does |
| --- | --- | --- |
| **RTTY speed** | 45.45 baud | Almost all amateur RTTY is 45.45 baud. 50 and 75 are offered for the few who use them |
| **RTTY shift** | 170 Hz | The distance between the two tones. Almost all amateur RTTY uses 170 Hz |
| **Reverse** | off | Swaps the two tones. Turn it on only for a station whose text reads as gibberish while its signal looks clean |
| **Unshift on space** | on | Returns to letters after a space, as most stations expect. Turn it off if numbers keep turning into letters |

Costas reads RTTY through fading that takes out one tone at a time, as long as the other tone stays above the noise.
