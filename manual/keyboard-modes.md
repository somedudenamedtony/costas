# PSK31, PSK63 and RTTY

!!! note "Preview: receive only"
    This build reads PSK31, PSK63 and RTTY but does not send them yet. **Call**, **Call CQ** and Enter say so and do
    nothing. Sending, macros and the conversation box come in a later build.

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
- **The waterfall** labels each signal with its station's call. Click a signal to read it, even before Costas has
  noticed it; it stays read while that station is quiet. For RTTY, click either of its two tones or between them.
- **Raw decodes** shows the text, a line per over or per line typed, with the audio frequency and signal strength.
- **dB** is measured in a 2,500 Hz bandwidth, the same scale as FT8, so −10 dB means the same in both.

The slot countdown is hidden in these modes, since there are no slots.

## Tips

- Costas reads a signal down to about −10 dB (PSK31). Below that it shows nothing rather than guessing, and it never
  shows text from plain noise.
- PSK63 is twice as fast and needs a few dB more signal.
- Keep the radio's receive filter at least 2.5 kHz wide so every signal in the waterfall can be read.

## RTTY settings

![RTTY on the Operate screen](images/rtty-operate.png)

RTTY has a few settings, in **Settings › PSK and RTTY**. The status line shows the ones in use, for
example "Reading RTTY 45.45 Bd, 170 Hz shift".

| Setting | Default | What it does |
| --- | --- | --- |
| **RTTY speed** | 45.45 baud | Almost all amateur RTTY is 45.45 baud. 50 and 75 are offered for the few who use them |
| **RTTY shift** | 170 Hz | The distance between the two tones. Almost all amateur RTTY uses 170 Hz |
| **Reverse** | off | Swaps the two tones. Turn it on only for a station whose text reads as gibberish while its signal looks clean |
| **Unshift on space** | on | Returns to letters after a space, as most stations expect. Turn it off if numbers keep turning into letters |

Costas reads RTTY through fading that takes out one tone at a time, as long as the other tone stays above the noise.
