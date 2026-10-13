# Raw decodes

The classic message list, for when you want to see every line the decoder printed. Press **Raw decodes** in the top
bar or ++ctrl+2++.

![Raw decodes](images/raw-decodes.png)

| Column | Meaning |
| --- | --- |
| **UTC** / **Local** | Slot time, `HHMMSS` |
| **dB** | Signal-to-noise ratio at your station. Your own transmissions show `Tx` |
| **DT** | Time offset of the signal in seconds. A median near zero means your clock is right |
| **Hz** | Audio offset in the passband |
| **Message** | The message exactly as decoded |
| **Country** | The sender's country, from the country file |

- The newest slot is at the top, with a blank line between slots.
- Messages to you are red; CQs are blue.
- Scroll down to read older lines and the list stays put while new slots arrive; **Jump to live** takes you back to
  the top.
- Type in **Filter by callsign or text** to show only matching lines.
- Double-click a line to call its sender.
- In FT8, most lines of a slot appear about 12.5 s into it, before the slot has ended, and the rest shortly after it.
  Costas keeps the decoder running between slots and starts on each slot early, as WSJT-X does. The station list and
  the slot's counts update once the slot is complete.
- A portable or compound call that was sent as a short code shows in angle brackets, for example
  `<PJ4/K1ABC> W7LIT DN40`. Costas fills in the call once it has heard it in full (in a CQ, say), so it can be called
  like any other station. Until then it shows as `<...>` and cannot be called.
- Programs such as JTAlert and GridTracker can colour callsigns here (for example, ones you need), as they do in
  WSJT-X. See [Other loggers](log.md#other-loggers).
