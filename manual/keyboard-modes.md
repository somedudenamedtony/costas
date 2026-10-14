# PSK31 and PSK63

!!! note "Preview: receive only"
    This build reads PSK31 and PSK63 but does not send them yet. **Call**, **Call CQ** and Enter say so and do
    nothing. Sending, macros and the conversation box come in a later build.

Pick **PSK31** or **PSK63** in the **Mode** menu. Costas tunes the radio to the band's PSK frequency (14.070 MHz on
20 m) in the same data mode it uses for FT8, so nothing else needs changing on the radio.

![PSK31 on the Operate screen](images/psk31-operate.png)

## What you see

PSK has no time slots: stations type whenever they like, and Costas reads every PSK signal in the receive passband at
once (up to 20).

- **Next in line, Repeats and Watching** work as they do for FT8. Costas finds each station's call in its text
  ("CQ CQ DE KP4JRS KP4JRS K", "W7LIT DE EA8TL") and ranks it by what you need. A station is listed as calling CQ
  for two minutes after its CQ, and the list refreshes every few seconds.
- **The waterfall** labels each signal with its station's call. Click a signal to read it, even before Costas has
  noticed it; it stays read while that station is quiet.
- **Raw decodes** shows the text, a line per over or per line typed, with the audio frequency and signal strength.
- **dB** is measured in a 2,500 Hz bandwidth, the same scale as FT8, so −10 dB means the same in both.

The slot countdown is hidden in these modes, since there are no slots.

## Tips

- Costas reads a signal down to about −10 dB (PSK31). Below that it shows nothing rather than guessing, and it never
  shows text from plain noise.
- PSK63 is twice as fast and needs a few dB more signal.
- Keep the radio's receive filter at least 2.5 kHz wide so every signal in the waterfall can be read.
