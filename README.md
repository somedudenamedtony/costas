<p align="center"><img src="src/Ft8Client.App/Assets/costas-256.png" width="96" alt="Costas icon"></p>

# Costas

**An FT8 and FT4 program for Windows that shows you stations, not messages.**

Costas uses the same decoder as WSJT-X, so it decodes as well and sounds identical on the air. What changes is the
screen: instead of a scrolling list of decoded lines, you get one row per station, ranked by what you need for your
log, whether the station is free to call, and whether it can hear you.

**[Read the user manual](https://somedudenamedtony.github.io/costas/)** ·
**[Download for Windows](https://github.com/somedudenamedtony/costas/releases/latest)**

![The Operate screen](manual/images/operate-idle.png)

## Features

- **Next in line**: the stations you need, best first. New country, then new band, new grid, new call, worked out
  from your QRZ logbook.
- **Repeats**: stations on the air now that you have already worked on this band, with when and where you last
  worked them, one click to call them again.
- **Hears you and Chance**: from PSK Reporter, whether each station (or someone near it) hears your signal, and a
  Good / Fair / Long shot rating.
- **One-click contacts**: click **Call** or press Enter. Costas runs the standard exchange, shows each step in a
  five-box strip, and logs the contact when it completes.
- **Calling you bar**: anyone who calls you is pinned above the list with an **Answer** button, and a station that
  replies after you gave up can be picked up again with **Resume**.
- **Logging and QRZ**: a local log with ADIF import and export, uploaded to your QRZ logbook and optionally eQSL as you go, with eQSL inbox and confirmation sync.
- **Maps**: who you hear (Heard here) and who hears you (Reach), centred on your grid.
- **Works with your other tools**: WSJT-X UDP for GridTracker, JTAlert and loggers.
- **Safe by design**: it never transmits on its own, never changes a message silently, and has a watchdog,
  band-edge check, clock check and PTT release on any fault.

<table>
<tr>
<td><img src="manual/images/contact-in-progress.png" alt="A contact in progress"></td>
<td><img src="manual/images/calling-you.png" alt="The Calling you bar"></td>
</tr>
<tr>
<td align="center">A contact in progress</td>
<td align="center">Someone calling you while you work another station</td>
</tr>
</table>

## Install

1. Download `Costas-Setup-<version>.exe` from the
   [latest release](https://github.com/somedudenamedtony/costas/releases/latest).
2. Run it. If Windows SmartScreen warns you (the installer is not code-signed yet), click **More info**, then
   **Run anyway**.
3. Start Costas. Setup walks you through six steps: your station, radio, audio, a transmit test, a clock check and
   QRZ.

Requires 64-bit Windows 10 (1809) or later. The decoder (`jt9` from WSJT-X) and radio control (Hamlib) are
bundled; you do not need WSJT-X installed. Costas checks for new builds at start and daily and offers to install them, keeping
your log and settings.

## Use it

1. Connect the radio by USB, tune to an FT8 frequency (14.074 MHz on 20 m) in USB or USB-D.
2. In setup, pick your radio model and press **Test connection**, then pick the radio's USB audio device and get the
   input level into the shaded band. Send the 2-second test tone into a dummy load.
3. Add your QRZ logbook API key so Costas knows what you have already worked.
4. Watch **Next in line**. Click **Call** on the top station, or press **Call CQ**.
5. Press **Esc** at any time to stop transmitting.

The [manual](https://somedudenamedtony.github.io/costas/) covers every screen and setting, with screenshots:
[setup](https://somedudenamedtony.github.io/costas/setup/),
[the Operate screen](https://somedudenamedtony.github.io/costas/operate/),
[making contacts](https://somedudenamedtony.github.io/costas/contacts/),
[maps](https://somedudenamedtony.github.io/costas/maps/),
[the log and QRZ](https://somedudenamedtony.github.io/costas/log/),
[settings](https://somedudenamedtony.github.io/costas/settings/) and
[troubleshooting](https://somedudenamedtony.github.io/costas/troubleshooting/).
Its source is in [`manual/`](manual/) if you prefer to read it here.

## Status

Costas is early software. Everything in the feature list is built and passes its automated tests (recorded audio,
a simulated radio and Hamlib's test radio), but it has had little on-air use yet. Use low power or a dummy load at
first and keep an eye on the radio. Problems and ideas: [GitHub Issues](https://github.com/somedudenamedtony/costas/issues).

## For developers

C# on .NET 10 with Avalonia 12. WSJT-X's `jt9` decodes and Hamlib's `rigctld` controls the radio, both as supervised
child processes. Rules for working on the code are in [`CLAUDE.md`](CLAUDE.md); the design is in [`docs/`](docs/):

| File | What it holds |
| --- | --- |
| [`docs/01-product-spec.md`](docs/01-product-spec.md) | Goals, users, scope, feature list by release |
| [`docs/02-ui-spec.md`](docs/02-ui-spec.md) | Every screen, component, state, keyboard shortcut and design token |
| [`docs/03-architecture.md`](docs/03-architecture.md) | Solution layout, components, threading, process model, simulation mode |
| [`docs/04-domain-logic.md`](docs/04-domain-logic.md) | Message parsing, station tracking, need tiers, ranking, contact state machine, transmit encoding |
| [`docs/05-integrations.md`](docs/05-integrations.md) | jt9, Hamlib, QRZ, PSK Reporter, WSJT-X UDP, time check, update check |
| [`docs/06-data-model.md`](docs/06-data-model.md) | SQLite schema, settings, files on disk, ADIF mapping |
| [`docs/07-milestones.md`](docs/07-milestones.md) | Build order, M0 to M8, with acceptance tests |
| [`docs/08-open-questions.md`](docs/08-open-questions.md) | Decisions assumed, facts not yet verified, known risks |
| [`mockups/`](mockups/) | The original approved screen designs |

### Build and run from source

```
third_party/fetch.sh            # or: pwsh third_party/fetch.ps1 on Windows (needs 7-Zip); fetches jt9 and Hamlib
dotnet build
dotnet test
dotnet run --project src/Ft8Client.App -- --simulate samples/ft8 --simulate-partner
```

The last line replays the sample recordings with a simulated station that answers your calls, so you need no radio.
Simulation never keys a radio, never uploads spots and does not write the decode journal or WAV files. Without
`--simulate` and with no radio set up, the app shows no data. `scripts/build-installer.ps1` builds the installer
locally.

Developer mode (hidden): in About, click the product name seven times. Settings then has a Developer section, and
Setup lists Hamlib's test radio ("Hamlib Dummy"), which runs the real `rigctld` path with transmit audio muted.

### Build status

Built through milestone M7, with FT4 receive and transmit from M8. Every automated acceptance test in
`docs/07-milestones.md` up to M7 passes. CI builds and tests on Linux and Windows, builds the installer, and
publishes every green build of `main` as a GitHub Release.

| Milestone | Automated | Still to do by hand or not built |
| --- | --- | --- |
| M0 skeleton, decode harness | Golden decodes for every sample through `jt9` | Windows DLL set for `jt9.exe` (V2) |
| M1 core domain | Parser, tracker, need tiers, ranking, country lookup | — |
| M2 log and QRZ | ADIF round trip, fake QRZ server; real read-only sync of the owner's logbook checked (396/396) | — |
| M3 receive, Operate (idle) | Slot clock, simulation, screen checked against the mockup under Xvfb | Live audio on Windows (WASAPI) |
| M4 radio and transmit | 53 FT8 + 53 FT4 vectors against `ft8code`/`ft4code`; waveform decodes through `jt9`; guards; `rigctld` crash recovery | On-air transmit with an IC-7300 |
| M5 contacts | Contact engine rules; ten simulated partner contacts end to end | On-air contacts |
| M6 PSK Reporter | IPFIX byte for byte; feed against a local broker incl. reconnect and de-dupe; query fallback | Live MQTT subscription (V7); spot upload seen on pskreporter.info |
| M7 setup, settings, log, interop | UDP datagrams for every type; settings migration; secrets kept out of settings, logs and the bundle | GridTracker on Windows; first-run timing with an IC-7300 |
| M8 FT4, hardening, release | FT4 vectors and decode round trip; user manual | 24-hour soak, code signing |

### The manual

The user manual is in [`manual/`](manual/), built with MkDocs Material and published to GitHub Pages by
[`.github/workflows/manual.yml`](.github/workflows/manual.yml) on every push to `main`. Preview it with
`pip install -r manual/requirements.txt && mkdocs serve`.

## Credits and licence

Costas is free software under the [GNU General Public License, version 3](LICENSE) or later, because it ships and
runs the WSJT-X decoder.

- Decoding by `jt9` from [WSJT-X](https://wsjt.sourceforge.io/) (K1JT, K9AN, G4WJS and the WSJT Development Group), GPLv3.
- Rig control by [Hamlib](https://hamlib.github.io/), LGPL.
- Transmit encoder ported from [ft8_lib](https://github.com/kgoba/ft8_lib) by Kārlis Goba, MIT.
- Country file `cty.dat` by Jim Reisert AD1C, [country-files.com](https://www.country-files.com/).
- Reception reports from [PSK Reporter](https://pskreporter.info/) (Philip Gladstone, N1DQ) and its MQTT feed by
  M0LTE. Logbook and lookups by [QRZ.com](https://www.qrz.com/).

Owner: Tony Shepherd, W7LIT. The product name lives in one constant, `AppInfo.ProductName`; the code namespace is
`Ft8Client`.
