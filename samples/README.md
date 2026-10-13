# Samples

Recorded or simulated slots for decoder tests and simulation mode. Each WAV is 12 kHz, 16-bit mono,
named `YYMMDD_HHMMSS.wav` (slot start, UTC). Each has a `.golden.txt` with the output of `jt9`
captured by `scripts/capture-golden.sh`; its `# args:` line holds the decode context used.

| File | Source | Content |
| --- | --- | --- |
| `ft8/181201_180245.wav` | WSJT-X sample set (sourceforge.net/projects/wsjt/files/samples/FT8), GPLv3 | Busy 20 m slot, RTTY Roundup exchanges |
| `ft8/210703_133430.wav` | WSJT-X sample set, GPLv3 | Busy slot, standard exchanges |
| `ft4/000000_000002.wav` | WSJT-X sample set (samples/FT4), GPLv3 | FT4 slot |
| `ft8/260101_000000.wav` | `ft8sim "CQ PJ4/K1ABC"` | Nonstandard call in a CQ |
| `ft8/260101_000015.wav` | `ft8sim "<PJ4/K1ABC> W7LIT DN40"` | Hashed call, unresolved in a separate run (V3) |
| `ft8/260101_000030.wav`, `260101_000045.wav` | `ft8sim "CQ K1ABC FN42" ... -22` | A-priori decodes (`a1`, `? a1`) |
| `ft8/260101_000100.wav` | `ft8sim "W7LIT K1ABC FN42" ... -22` | A-priori decode with low confidence (`? a3`) |

| `psk31/synthetic-busy-20m.wav` | Costas's `PskModulator` with Gaussian noise (seed 2026) | Five overs from four PSK31 stations, one weak; sent text in `.expected.txt` |
| `psk63/synthetic-two-stations.wav` | Same | Three PSK63 overs from two stations |

The `psk31`, `psk63` and `rtty` folders hold continuous keyboard-mode audio, not slots: the `jt9` golden tests and FT8
simulation skip them, and `--simulate` plays them in a loop while the Mode menu is on that mode.

Callsigns in the simulated files are examples, used only by tests.

`clublog/mostwanted.json` is Club Log's most-wanted list as downloaded on 2026-10-12 from `https://clublog.org/mostwanted.php?api=1`, for the parser tests.
