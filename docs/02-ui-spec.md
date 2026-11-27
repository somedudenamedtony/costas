# 02 · UI spec

The mockups in `mockups/` are the visual reference. This document says what each part is, where its data comes from and how it behaves. Where they disagree, this document wins.

## Look

Native Windows 11. Use Avalonia's Fluent theme and stock controls. Classic, the default skin, follows the system light or dark theme and the system accent colour. Settings › Appearance also offers Ocean (cool blue), Forest (soft green), and Amber (warm sand and copper) skins, each with light and dark palettes. The theme choice (system, light or dark) is independent of the skin. Light and dark swatches preview the skin; Save applies it to all desktop windows without a restart. Modern Radio, Classic Transceiver, Vintage Receiver, SDR Console, Workbench and Darkroom (owner pick 2026-11-26) additionally provide distinct faceplates, panel geometry, typography and control styling. Classic Transceiver uses gray and black surfaces and a vector seven-segment lime green LCD readout, including dim inactive segments; the frequency stays available in its button's accessible name. Their large frequency displays and dial buttons open the existing dial editor, obeying the same transmit lockout. Segmented slot strips and the vintage needle display the real slot progress, never simulated signal strength. Instrument faceplates replace the small top-bar frequency readout and fit the minimum 1100 × 720 window. Switching back to a palette skin removes the instrument styles and restores the standard readout. Workbench and Darkroom draw their faceplates from the skin palette, so they follow light and dark, and use bundled OFL typefaces (IBM Plex Sans and Mono; Red Hat Text and Mono). Darkroom keeps its text at 7:1 and secondary text at 5:1 or better against the panels (owner request 2026-11-26: enough contrast to read everything); the waterfall's Night red palette matches it. Skins change surface, text, accent and selection tokens through Fluent palettes; transmit, caution and success colors retain their meanings. The values below are Classic's light theme with the default accent, for reference.

| Token | Value | Use |
| --- | --- | --- |
| Window background | `#F3F3F3` | Behind cards |
| Card background | `#FFFFFF`, border `#E0E0E0`, radius 8 | Each section |
| Control | height 32, radius 4, border `#D1D1D1` | Buttons, dropdowns |
| Text | `#1A1A1A` | Primary |
| Secondary text | `#5F5F5F` | Labels, captions |
| Accent | system accent (`#005FB8`) | Selected state, primary button, "New DXCC 20 m", "new grid" |
| Caution | `#8A4B00` | "New DXCC", upload waiting |
| Critical | `#C42B1C` | Transmitting, Abandon, Halt while transmitting |
| Success | `#0F7B0F` | Connected indicators |
| Selected row | `#E5F1FB` with a 3 px accent bar on the left | Top of the line, selected station |
| Font | Segoe UI Variable, 13 px body, 15 px section titles, 40 px current callsign | |
| Message font | Cascadia Mono, 12 px | Only for literal FT8 message text |
| Numbers | Tabular figures in every column of numbers | |

Colour is never the only signal: every state also has text.

## Window

One window, minimum 1100 × 720, default 1440 × 900, remembers size and position. Layout from top to bottom: top bar, main area, bottom bar. The main area has a wide left column and a right column of about 300 px, widening to 460 px when the window is 1600 px or wider so the map has room. Below 1100 px wide the right column drops under the left.

### Top bar

| Element | Behaviour |
| --- | --- |
| Band dropdown | Lists bands from the frequency table. Changing it sets the dial frequency through the rig service, clears the station list for the old band and loads log totals for the new one. Disabled while transmitting. The closed box shows the band only. The open list adds, per band, where stations near me are heard right now (`04-domain-logic.md` section 14): an activity bar and "1,240 spots" (or "No spots"), six continent chips NA SA EU AF AS OC shaded by spots, and "You need Brazil +2" in caution colour. Tooltips give the source and the full list of needed countries. With no reports (feed or setting off, nothing received yet) the list shows band names only. A band whose reports are not being received says why instead of "No spots": "Set your grid in Settings" when my configured grid is missing or invalid, "Checking soon" for a band not yet sampled after the feed fell back to the current band; sampled bands show estimates as "~1,200 spots" |
| Mode dropdown | FT8, FT4. Changes slot length and dial frequency. Disabled while transmitting |
| Frequency | Dial frequency read from the radio, `14.074.000` format, MHz label. A click opens a menu (product review 2026-10-12): a box to type a frequency ("14.090", "14090" kHz or "14.090.000") with Tune, "Your frequencies" (saved dial frequencies as buttons, each with Remove) and "Save the current frequency". Tuning changes band when the frequency is on another band; frequencies outside the band plan or on a band without transmit limits are refused with a sentence. Disabled while transmitting |
| View tabs | Operate, Raw decodes, Reach, Log, Awards, and Contest while a contest is picked (amber badge with its CQ modifier). Ctrl+1 to Ctrl+5, Ctrl+6 for Contest |
| Station profile | Only with more than one station profile (owner request 2026-11-18). A chip left of the clock with the callsign and profile in use, "K1UTA · Ash ▾", and the callsign in the window title ("Costas · K1UTA"). Amber for a minute after a phone switched the profile (D25). A click opens a menu of the profiles, the one in use ticked; choosing one switches only while the station is idle (otherwise the status line says why), as Settings does; "Edit profiles…" opens Settings on the Station page |
| Clock | UTC `HH:MM:SS`. Turns caution colour with a tooltip when the clock offset exceeds 0.5 s; critical above 2 s |
| Menu button | Mini mode (Ctrl+M), Settings, Station profile, Diagnostics, Keyboard shortcuts, About |

### Bottom bar

| Element | Behaviour |
| --- | --- |
| Status text | One plain line: "Not in a contact. Enter calls ZL2RPA, the top of the line." / "Transmitting to ZL2RPA." / "Waiting for ZL2RPA." / fault text in critical colour |
| Radio controls | Power, amp and tuner, Tune, Tx offset and Auto-select (described in the rows below). Shown only on views other than Operate; on Operate the contact card's Radio row has them (owner pick 2026-11-26) |
| Tx offset | "Tx 1650 Hz ▾" with the full "Tx offset 1650 Hz · clear" as its tooltip. Click opens a small flyout: automatic (default) or a manual value |
| Call CQ | Starts calling CQ in the next slot of the chosen parity. Becomes "Stop CQ" while running. Shown only on views other than Operate; on Operate the contact card's on-air panel has it |
| Tune | Owner request 2026-10-12: a toggle with a 10 s limit that starts the radio's own tuner when it has one. First it sends Hamlib's TUNE operation (`G TUNE`) after the radio and band-edge guards; if the radio accepts, the button reads "Stop tune" and the status line "The radio's antenna tuner is tuning." for 10 s (the radio runs its own cycle; Stop tune or Halt sends PTT off). If the radio has no TUNE operation or refuses it, a steady tone at the Tx offset (centred on where an FT8 signal sits) for up to 10 s (the Tune watchdog), PTT on at once, after the same radio, audio and band-edge guards as any transmission. Reads "Stop tune" while the tone is on. Disabled while CQ, a contact or another transmission is under way; a transmission the operator starts meanwhile replaces the tone |
| Amp and tuner | Customer request 2026-11-18, FlexRadio only (`05-integrations.md` section 2a). Shown only while the FlexRadio at the address in Settings › Station › FlexRadio amp and tuner (or a radio set up as SmartSDR Slice A to H) reports a paired amplifier or tuner: a button reading "Amp: Standby  Tuner: Operate ▾" (each device as a label with a colon, its state in bold), drawn like the other bottom-bar buttons so it reads as the way into the controls (owner, 2026-11-18). Amp text in caution colour in operate, critical while the amplifier transmits or has a fault, normal otherwise. A click opens a flyout: the amplifier's name and state with **Operate** and **Standby**; the tuner's name and state (Operate, Bypass, Standby, Tuning) with **Operate**, **Standby**, **Bypass** and **In line**. Each button is enabled only when it would change something; all are disabled while anything is on the air, while the amplifier transmits and while the tuner tunes, and a refusal says why in one line. Nothing switches without a click |
| Halt Tx | Always enabled. Drops PTT immediately and stops sequencing. Critical colour while transmitting. Esc does the same |
| Slot progress | Label "Receiving" or "Transmitting", seconds to next slot, a 4 px bar. Critical colour while transmitting. Shown only on views other than Operate; on Operate the contact card's on-air panel has it |
| Meter warning | Only when the last transmission's ALC or SWR crossed a threshold (`04-domain-logic.md` section 11): "SWR 3.4 · ALC 70%", caution colour, critical when SWR is 3 or more. The tooltip and screen reader text give the full advice. Hidden when the meters read within limits. The live meters are in the on-air bar (owner request 2026-10-28: the bottom bar is out of sight) |
| Radio meters (on-air bar) | Owner request 2026-10-27, moved to the red on-air bar 2026-10-28. "PO 60 W · ALC [bar] OK · SWR 1.3" beside Halt, white on red: the radio's PO meter in watts, ALC as a 10-segment bar (caution from Hamlib 0.5) with "OK" or "High" (not a percentage: Hamlib's ALC scale is not the radio's), and SWR, from each 0.5 s reading while a transmission or tone is on the air. SWR from 2 and "High" in yellow (#FFD27A). A meter the radio does not report is left out. Instrument skins show the same meters on the faceplate display and hide this copy. Display only (D13) |
| Indicators | Radio, audio, QRZ, PSK Reporter: a dot and a word each. Click opens Diagnostics. DX cluster joins them only once it is turned on in Settings (C4): ok when logged in to the node, retrying while connecting or after a drop, down when unreachable or the node asks for a password |

## Operate view

### Why it's quiet

Owner request 2026-11-18. When FT8 or FT4 has gone quiet, one line under the top bar (below the Hunt list alert, in the same caution-bordered card style) says the most likely reason and, where there is one, the fix. Operate view only; hidden on the other views and in PSK and RTTY. The rules are in `04-domain-logic.md` section 18.

- **When:** after two received slots in a row with no decodes (30 s in FT8, 15 s in FT4). Two, so both an even and an odd slot have passed with nothing heard: one empty slot is common on a quiet band, or when everyone is on the other parity. Slots I transmitted in are not counted. A band, mode or dial change starts the count again. The line goes the moment anything is decoded.
- **Never with no radio set up:** with simulation off and no radio configured nothing real is received, so nothing is shown (rule 2). With a radio set up but not connected the line defers to the yellow no-radio banner, except for an audio fault, which the banner does not cover.
- **Advice only:** the line never changes the radio. Its one button, **Go to 14.074** (the band's actual usual frequency; never called "Tune", which sends a tone), tunes through the same path as a frequency typed in the frequency menu (`AppHost.TuneDialAsync`: changes band if needed, sets USB data), only when clicked, and is hidden while transmitting.

Label "Why it's quiet" in caution colour, then the text. Causes, in the order they are checked:

| Cause | Text | Button |
| --- | --- | --- |
| No audio arriving | No decodes: no audio is arriving from the radio's sound device. Check that it is plugged in and that no other program is holding it. | — |
| Silence (all zero) | No decodes: the radio's sound device sends only silence. Turn up the radio's USB audio output and check that it is not muted. | — |
| Too quiet | No decodes: the receive audio is too quiet (-52 dBFS). Turn up the radio's USB audio output or the computer's recording level. | — |
| Clipping | No decodes: the receive audio is clipping. Turn down the radio's USB audio output or the computer's recording level. | — |
| Dial off | No decodes: the dial is on 14.090, not the usual FT8 frequency for 20 m. | Go to 14.074 |
| Wrong radio mode | No decodes: the radio is in LSB, and FT8 needs USB data. Change it on the radio, or Go to 14.074 sets it. | Go to 14.074 |
| Narrow filter | No decodes: the radio's receive filter is 500 Hz wide, and FT8 signals spread over about 3000 Hz. Widen it on the radio to about 3000 Hz. | — |
| Decoder down | No decodes: the decoder (jt9) is not running: jt9 not found. Diagnostics has the details. | — |
| Decoder failing | No decodes: the decoder (jt9) is not working: jt9 exited with code 2. Diagnostics has the details. | — |
| Clock off | No decodes: your clock is 3.1 s off, and decodes need it within 2 s. Sync the computer's clock, or turn on Clock correction in Settings › Transmitting. | — |
| Nothing found | No decodes in the last 4 slots. Audio, dial, radio mode, filter, decoder and clock look right, so 20 m may just be quiet. | — |

Values in the examples (14.090, LSB, -52 dBFS, the decoder's message, 3.1 s, 4 slots, 20 m, FT8) are filled in from the live state. Squelch is not checked. The phone page shows the same line and button (see Phone).

### Big and small windows

Owner request 2026-11-16: the compact layout (owner decision 2026-11-14) is for small screens only. The Operate view picks its layout from its window's client size in device-independent pixels (`OperateViewModel.IsCompactSize`) and switches live as the window is resized:

- **Compact** when the window is narrower than 1280 or shorter than 900 (a 13-inch laptop at about 1280 × 720, a 1080p laptop at 125% at 1536 × 864, a 1600 × 900 screen maximised): the one-line idle card with the band strip, the right column as one tabbed card with the fold rail, and the slim update bar.
- **Roomy** otherwise (the 1440 × 900 default window, a maximised 1080p or larger monitor): the full idle card with the band summary card under it, the right column's cards stacked in a scrolling column (no tabs, no fold), and the full-height update bar. This is the layout from before 2026-11-14.

The operator can override it in Settings › Appearance › **Operate layout** (`Appearance.Layout`): automatic (default, by window size as above), roomy or compact. It follows the window, not the view, so a taller waterfall or a faceplate skin never flips it. `Window.SideTab` and `Window.SideCollapsed` are kept while roomy and apply again when the window is small.

### Top slot, idle: band summary

Mockup: `main-idle`. Three groups. Roomy layout: a card below the idle contact card, the three groups side by side (Heard from drops under the other two on a card narrower than 1000 px); pinned with the card while both fit whole, otherwise it moves to the top of the call list and scrolls with it. Compact layout: shown on the **Band** tab of the right column (owner decision 2026-11-14; as a card it pushed Next in line off a 13-inch laptop screen). The tab is available in a contact too.

**Band strip** (compact layout only). While idle, the contact card carries the summary in one line under its own: "20 m now", the four counts ("212 heard", "14 countries", "31 CQ", "198 you need" in accent), a 14 px bar per continent labelled NA, EU, AS, OC, SA, AF (tooltip "Europe: 8 stations"), the log line "19 countries · 46/50 states · 142 grids" (trimmed when short of room; tooltip gives the full log rows with the states still needed) and a **More ▾** link that opens the Band tab. The strip is hidden while the Band tab is open and showing, so nothing is on screen twice; it shows again when the right column is folded or on another tab.

**"20 m right now"** (title uses the current band)

| Value | Definition |
| --- | --- |
| Stations heard | Distinct callsigns decoded on this band in the last 5 minutes |
| Countries | Distinct DXCC entities among them |
| Calling CQ | Stations whose state is CallingCq |
| You need | Stations in need tiers 1 to 4. Accent colour |
| Caption | "N decodes in the last slot · M per slot over the past hour" |

**"Heard from"**: six columns (N. Am., Europe, Asia, Oceania, S. Am., Africa), each with a count and a bar scaled to the largest count. Continent comes from the country file.

**"Your log on 20 m"** (current band, from the local log after QRZ sync)

| Row | Definition |
| --- | --- |
| Countries | Entities worked on this band · entities confirmed on this band |
| US states | States worked on this band out of 50, then up to three missing ones, then "and N more" |
| Grid squares | Distinct 4-character grids worked on this band |
| This session | Contacts this session · new DXCC · new band DXCC. A session starts at app launch or after 2 hours with no contact |

**Band advice** (C5, built 2026-11-18; rules in `04-domain-logic.md` section 14a). Under the three groups, across the card's width (roomy layout), and under the caption on the **Band** tab (compact layout): at most two lines, shown only when the evidence supports them, nothing at all otherwise.

- The open line, semibold in accent colour: "15 m is open to Europe from your area: 23 stations there heard 6 stations near you in the last 15 min." (another band, from stations near me), or "20 m is open to Europe for you: 12 stations there heard you in the last 15 min." (this band, from reports of my own signal). Two continents: "15 m is open to Asia and Europe from your area: 9 and 6 stations there heard stations near you in the last 15 min." A sampled band says "in 30 seconds of listening" instead of "in the last 15 min". Needed countries add " 3 countries you need on 15 m hear your area." or " Mongolia, which you need on 15 m, hears your area."
- The closing line, secondary text: "20 m is closing to Europe: 1 station there heard stations near you in the last 15 min, 14 in the 15 min before."
- Each line's tooltip gives, per continent, how many stations heard how many, the radius ("within 300 miles", or 480 km with kilometre units), how long ago the newest report was, every needed country, the threshold that was met, and "Advice only: Costas does not change band for you."
- Display only: no Go to band link, no effect on band, ranking or who is called. With the feed or band activity off, no grid, or no reports (no radio, no network), it shows nothing.

### Top slot, contact card

Mockup: `main-in-contact`. Always the first card on the Operate view, so the on-air panel (countdown and Call CQ) is always in the same place. It is pinned above the scrolling area (owner request 2026-10-26): only Next in line, Repeats and Watching scroll. On a short window the pinned cards give way and scroll on their own so the call list keeps at least 220 px. From the moment the operator calls a station or answers a caller until the contact is logged or abandoned, it shows the contact and the band summary is hidden. After logging it stays for one slot showing "Logged", then goes idle.

Idle (no contact): caption "Not in a contact" (or "Calling CQ" with the subline "Waiting for a station to answer." while CQ is running), "—" in place of the call, the subline "Call a station in Next in line, or call CQ.", reports "--", no conversation strip, and Resend, Log now and Abandon disabled. The band summary card shows below it (roomy layout). Owner decision 2026-11-14, compact layout only: idle, the card folds to one line: the caption in semibold with the subline under it, and on the right the slot countdown, Call CQ, the CQ picker and Auto-call in a row; the "—", the two empty reports, the on-air panel's border and the Resend, Log now and Abandon buttons are hidden (they have nothing to act on). The band strip sits under it in the same card. The full card returns the moment a contact starts.

| Element | Content |
| --- | --- |
| Caption | "Contact in progress", or "Calling", "Logged", "No reply", "In another contact" (the subline then gives the reason: "N8WCR answered K1ABC instead. Stopped calling." or "N8WCR is in a contact with K1ABC. Not calling.") |
| Callsign | 40 px, with the need tag beside it |
| Subline | Name from QRZ lookup (blank until it arrives) · country or state · distance |
| On-air panel | A bordered panel on the right of the card that holds everything about the next transmission, described below |

**On-air panel**: one row of readouts above one row of buttons, and under them the Radio row (owner pick 2026-11-26, "Rethink radio controls placement", option B).

| Element | Content |
| --- | --- |
| You sent / You received | The reports with "dB" after them. Shows "--" until exchanged |
| Slot | "Transmitting" or "Receiving", the seconds left, and a progress bar (critical while transmitting). It always counts down to the next slot boundary; the whole slot a transmission went out in reads "Transmitting", even after the audio ends about 2 s early (owner request 2026-10-26) |
| Call CQ / Stop CQ | The same command as Ctrl+Q and the bottom bar's Call CQ button (shown on other views) |
| CQ modifier | Owner request 2026-10-12. A dropdown beside Call CQ: CQ, CQ DX, CQ NA, CQ SA, CQ EU, CQ AS, CQ AF, CQ OC, CQ POTA, plus the Settings value if it is another one. Picking saves it as Settings › Transmitting › CQ modifier, so both always agree; it applies from the next CQ sent |
| Resend | Sends the current step again in my next slot |
| Log now | Logs the contact as it stands. Enabled once both reports are exchanged |
| Abandon (Esc) | Stops transmitting, ends the contact, logs nothing |
| Radio row | Under a divider, "Radio" then the radio's settings, moved from the bottom bar: power ("50 W ▾", only when the radio reports it), the FlexRadio amp and tuner button (only when paired), Tune, "Tx 1650 Hz ▾" (adds "manual" or "busy" when they apply; the whole "Tx offset 1650 Hz · clear" line is the tooltip; the same flyout as before) and Auto-select. Each control behaves exactly as described under Bottom bar. The row is never wider than the button row above it (at least 480 px) and wraps instead. On the folded idle line (compact layout, no contact) it is replaced by one "Radio 50 W ▾" button whose flyout holds the same row. On views other than Operate the bottom bar shows the row instead, so one copy is on screen |

**Conversation strip**: one box per step in a row, mine raised 12 px, theirs lowered 12 px. Above the boxes a progress track has one numbered marker per step: a tick on a filled accent marker once the step is done, a filled critical marker while it is on the air, an accent ring for the step being waited for, and a grey ring ahead. The track is accent up to the furthest step reached.

Each box has three lines: who ("You" or the DX call, semibold) with the UTC time on the right, the literal message, and a status.

| Box state | Look | Time | Status |
| --- | --- | --- | --- |
| Done | Thin border, no fill | When sent or received | "Sent" or "Received" |
| Now, transmitting | Critical fill, white text | When the transmission started | "Sending · 7 s left" |
| Now, waiting | Accent tint, 2 px accent border | The slot the reply is due in | "Waiting for reply" |
| Now, mine, not yet on the air | Accent tint, 2 px accent border | Blank, or the slot time when it is not the next slot | "Sending next slot", or "Sending at HH:MM:SS" when my next slot is later (clicked too late in my own slot) |
| Upcoming | Dashed border, secondary text | Blank, or the slot it is due in when it is the next reply | "Next", "Expected", or "Then logged" for my last step |
| Repeated | As above with "×2" after the verb | Latest time | When a step was sent or received more than once |

The engine counts a message as sent when its transmission starts. While that message is still on the air its box shows as transmitting and the reply box after it stays upcoming; the reply box is highlighted as waiting only once the transmission has ended.

Clicking one of my upcoming boxes jumps the sequence to that step (manual override). The boxes are generated from the contact state machine's step list, so the CQ-run flow shows its own six steps.

**Contact station details** (beneath the horizontal conversation in the contact card): a shaded strip groups the station name, licence and QRZ/photo links; location, distance, county and local time; grid, bearing and CQ/ITU zones; and QSL methods and most-wanted rank. Values are emphasized above secondary details, and groups wrap on narrower windows. Missing optional facts are omitted, including the QSL/awards group when empty. Lookup status and Who else is calling appear below the groups when available; Who else is calling sits in an amber attention box (caution background and border, heading in the caution colour) so a pileup is noticed before the operator keeps calling. The contact's station is looked up on QRZ when the contact starts. These facts no longer occupy the top of the right column.

### Next in line

A grid with a header row. One row per station that is needed (tiers 1 to 4), callable (see `04-domain-logic.md`) and passes the filters. Sorted by the ranking rule. At most 12 rows; the rest are counted in a footer link "and N more".

| Column | Content |
| --- | --- |
| # | Rank, starting at 1 |
| Call | Callsign, semibold. While other stations are calling it (current callers in the pileup tracker, `04-domain-logic.md` section 12), a small amber tag under the call: "3 calling", in the attention colours of the Who else is calling box. Tooltip and screen reader text name the callers, strongest first, with their dB at my station: "Calling VK3XYZ at 18:30:00: W6ABC -06, K7XYZ -11." (at most 6 named, then "and N more"). Updated on every snapshot, also while the list is held still. Picks alone do not tag a row. Repeats rows get the same tag (owner request 2026-11-01) |
| Where | Country, or "State, USA" / "Province, Canada" when known (distance has its own column since 2026-11-26, owner request; the tooltip gives place and distance together). With a city from QRZ: "Springfield, IL, USA", "Ottawa, ON, Canada", "Wellington, New Zealand". City and state come from QRZ: stations in the list are looked up once (see `05-integrations.md` §4) and the answer is kept in the local cache, so it shows again at once after a refresh or restart. A row updates as soon as its lookup arrives, without waiting for the next re-rank |
| Distance | Great-circle distance in the operator's unit, "1,550 mi" or "2,494 km", right-aligned, 76 px. Empty while the station's grid is unknown. Hidden on a narrow window after Signal and before # |
| Why | Need tag: New DXCC (caution, bold), New DXCC with the band for a DXCC entity in the log but not on this band, "New DXCC 20 m" (accent, bold; "New band DXCC" where no band is known; owner request 2026-11-14), New grid with the station's 4-character grid, "New grid DO62" (accent, bold), New call (plain). A new band has the tooltip "Canada is in your log on 40 m and 15 m, not on 20 m yet" (owner request 2026-10-12). A new country on Club Log's most-wanted list also gets a rarity meter after the tag: five small cells, filled in caution colour up to its level, with the tooltip and screen reader text "Rare: #61 on Club Log's most-wanted list" (`04-domain-logic.md` section 15). Under the tag, display-only lines in secondary text when they apply: the park (POTA), the DXpedition (NG3K) and "Spotted 4 min ago" when the DX cluster spotted the station on this band in the last 30 minutes (tooltip "Spotted 4 min ago on 20 m by DL1ABC · FT8 -12dB"; `05-integrations.md` section 5b). None changes the order. The column is 160 px wide in Next in line and Repeats |
| Doing now | "Calling CQ, 6 slots" · "Calling CQ DX, 2 slots" · "Calling CQ, 4 slots, fading" · "Calling you" · "Calling CQ, missed last slot" / "Calling CQ, missed 3 slots" for a CQ caller kept listed after a miss |
| Last heard | Time since the end of the station's last decoded transmission: "45 seconds ago", "2 minutes ago". Counts up between slots, also while the list is held, secondary text |
| dB | Their latest SNR at my station |
| Signal | Sparkline of their SNR over their last 6 own slots, oldest on the left, gaps where they were not decoded. The scale fits the station's readings but spans at least 12 dB. Tooltip and screen reader text give the values |
| Chance | Good (bold), Fair, Long shot, followed in secondary text by what it is based on (owner request 2026-10-12, replacing a separate Hears you column): "hears you at -22" (direct report in the last 15 min), "heard in Japan at -17" (best report from the same entity, state or grid field), "no reports yet", or nothing without PSK Reporter. 230 px wide; a long reason is trimmed and shown whole in the tooltip |
| Last worked | The most recent log contact with the call on any band or mode, as in Repeats ("2026-10-03 · 40 m"), or "Never"; tooltip as in Repeats |
| Call | Button. Calls that station |

Behaviour:

- Row 1 is highlighted as the selected row when nothing else is selected. Up and Down move the selection. Enter calls the selected row (in a contact, nothing). Double-click calls that row. During a contact a double-click does nothing, unless the operator turned on "Double-click a station during a contact to abandon it and call the new one" (Settings, Transmit; off by default; FT8 and FT4 only): then it abandons the contact and calls the station double-clicked (04-domain-logic.md, "Switching stations"; PR #150, owner sign-off 2026-11-14). Double-clicking the station in contact does nothing. A Call button clicked during a contact always ends that contact properly first, the same way.
- Rows re-rank only when a slot's decodes arrive, and never while the pointer is over the list or within 300 ms of a click. A pending re-rank shows a "List updated" pill that applies on click or when the pointer leaves. With an early decode pass (FT8), the list is ranked when it finishes, about 13 s into the slot, roughly 2 s before the full decode; when the full decode of that slot arrives, it does not reorder the list: rows shown keep their order and new stations go in at their rank, while a station no longer in the line drops out (PR #150, owner sign-off 2026-11-14). Repeats follows the same rule. A band or mode change is the exception: Next in line and Repeats empty at once, and a slot recorded partly on the old band is not decoded into them (owner request 2026-11-14).
- Selecting a row loads its QRZ lookup. Clicking the station's call, or the I key, opens a details strip under the row (a click elsewhere on the row only selects it, owner request 2026-11-15 from Reddit feedback; an open strip follows the selection), across the full width of the list (owner request 2026-11-14, replacing a floating card to the right of the list, which was long, hard to scroll and closed only with its ✕). The strip lays the details out in columns that wrap on a narrow window: who and where (name with nickname, location, county, grid, bearing, the station's local time, licence class); QSL and awards (how it QSLs: LoTW, eQSL, paper, manager; CQ and ITU zones; the most-wanted rank, "Rare: #61 on Club Log's most-wanted list"; park; DXpedition; the newest DX cluster spot on any band, "Spotted 4 min ago on 15 m by DL1ABC · FT8 -12dB"; when the operator hears its country); **Your contacts** (the latest 3 contacts with that call: date, band, mode and a ✓ when confirmed, then "+N more"); and **Who else is calling** (when anyone has been heard calling the station, or it has answered callers in the last 10 minutes; `04-domain-logic.md` section 12): the headline, the current callers (call, dB at my station, audio offset), where my transmit offset sits among them, and how the station has been choosing, in the amber attention box. Under the columns, the links: "Open on QRZ.com", "Photo" when QRZ has one, "Hunt this call" and "Hunt <country>". Lines and columns with no data are left out; without a lookup it says "No QRZ details yet". The strip refreshes when the lookup arrives. Clicking the row again folds the strip and opens it again (the row stays selected, so Enter still calls it; the second click of a double-click leaves it as it is), selecting another row moves it there, and Esc closes it as well as doing what it always does. A click selects the row at once, but the strip opens (or folds) only once the system double-click time has passed with no second click, so a double-click never opens it: it calls the row its first click picked, even if a strip closing elsewhere moved the rows so the second click lands on another row; one that starts in the strip calls nobody. When the station leaves the line the strip closes.
- Local time uses QRZ's standard offset (`GMTOffset`) and daylight-saving flag (`DST`). Daylight saving is applied with the US/Canada rule or the EU rule; anywhere else that observes it, local time is left out rather than guessed.
- Empty state: "Nobody you need is calling CQ right now." with the Watching list still shown.
- Resizable columns (owner request 2026-10-31): dragging the edge between two headers moves that edge: the column on one side grows by what the column on the other side gives up, so no other column moves and a column can grow even when the table has no spare room (owner request 2026-11-14; before, every drag took its room from Doing now, which on a narrow window was already squeezed to nothing, so no column could grow). The widths above are the defaults. Beside Doing now, Doing now gives or takes the room and keeps at least 60 px; the Call column is not resized. Columns are at least 24 px. Next in line and Repeats share one set of widths. Double-clicking an edge restores the defaults of the columns on either side of it. Widths are saved in settings (`Window.ColumnWidths`) when a drag ends; saved widths that no longer match the columns are ignored. Edges show as a faint divider line in the header, accent while pointed at or dragged. Auto-size (owner request 2026-11-26): after each layout pass every row reports how wide its Where, Why and Doing now text is (measured without trimming). When everything fits, room Doing now has beyond its own widest text (at least 60 px) goes to Where and Why where their text is cut off, shared in proportion to what each is short, never past what the text needs. A column the operator has dragged, or saved at a width of its own, is not auto-sized; double-clicking its edge hands it back. Dragging a widened column starts from its width on screen. Watching auto-sizes its Where and Why the same way, keeping room for the Call when free button. Widths saved before the Distance column existed get it inserted at its default (Where gives up the 76 px, never below its new 160 px default unless the operator had it narrower).

### Watching

Stations that are needed but not callable now. Columns, with a header row (Call, Where, Distance, Why, Signal, Doing now) whose edges resize them as in Next in line: Call, Where, Distance, Why, signal sparkline (as in Next in line), reason. Reasons: "Went quiet 45 seconds ago", "In a contact with EA5HVK", "Finishing a contact with K7RGB". Rows are secondary text. Maximum 8 rows. Each row ends with a "Call when free" button, styled like the Call buttons in Next in line (owner requests 2026-10-26 and 2026-10-27): it marks the station to be called once its current contact is over (rules in 04-domain-logic.md, "Call when free"); a marked row adds "· calling when free" to its reason and its button reads "Cancel". A marked row is always listed, also past 8 rows, and stops the list folding. When no row is a new country or new band, the list folds to one line under the title ("12 needed stations busy: 11 new calls, 1 new grid") with a "Show all" link; once opened it stays open ("Fold" link) until the operator folds it (product review 2026-10-12).

### Repeats

Directly under Next in line. Stations heard now that are already worked (tier 5: this call on this band and mode), so the operator can work them again. Title "Repeats", subtitle "Heard now and already in your log on 20 m FT8." Columns are the same as Next in line, at the same widths so the two tables line up (owner request 2026-10-11): #, Call, Where, Distance, Why ("Not confirmed" in accent bold, or "Confirmed" in secondary text, for this call on this band and mode: the usual reason to work a station again; owner request 2026-10-12), Doing now (or, when not callable, the Watching reason in secondary text; "Went quiet 45 seconds ago" counts up every second with Last heard), Last heard, dB, Signal sparkline, Chance with its reason, Last worked ("Today · 20 m", "Yesterday · 40 m", "2026-10-03 · 20 m": the most recent log contact with the call on any band or mode; tooltip gives the contact count, exact UTC time, band, mode and confirmation), and a Call button ("Answer" when the station is calling me; always enabled, also when not callable, owner request 2026-10-12; double-clicking a row calls it as in Next in line, PR #150, owner sign-off 2026-11-14; a station in a contact with someone else is still not called, the contact ends at once as "In another contact", see 04-domain-logic.md). Callable stations first, then those not confirmed, then the line's ranking order. At most 8 rows, then "and N more". The list is held still while the pointer is over it or within 300 ms of a click, as Next in line. Clicking a row's call opens the station's details strip under it, with the same content and the same ways to close it as in Next in line (owner request 2026-11-14). One strip is open at a time across the two lists: opening one in Repeats leaves no row selected in Next in line, so Enter calls row 1 as when nothing is picked. Empty state: "Nobody you have worked on 20 m is on the air right now."

### Right column

Roomy layout: a column that scrolls, with two cards: Heard here (map, then Band activity) with Your signal under a divider, then Worked this session. Compact layout (owner decision 2026-11-14): one card with four tabs, **Band** (the band summary above), **Heard** (Heard here and Band activity), **Signal** (Your signal) and **Session** (Worked this session), one showing at a time, each scrolling on its own. It opens on Heard the first time; the tab is remembered (`Window.SideTab`). A **»** button at the top right folds the column to a 36 px rail (`Window.SideCollapsed`, remembered) so the call list takes the full width and the columns shed on a laptop come back. The rail has **«** to open it, and two labels read bottom to top that open their tab: "20 m · 198 you need" (Band) and "Your signal · 146 heard you" (Signal; "Your signal" with no reports). Below 1100 px, where the column lies under the list, the folded rail is one line.

**Heard here** (first card): a map of the stations I decoded in the last 15 minutes, with choices that are remembered in settings (`Map`):

| Choice | Options |
| --- | --- |
| View | Around me (azimuthal, centred on my grid, rings as in Reach) · World (flat map) |
| Show | Everyone heard · Needed only · Calling CQ |
| Colour by | Need (new country caution, new band or grid accent, new call primary, worked grey) · Signal (strong, fair, weak by size and shade, as in Reach) · Last heard (in the last minute, 1 to 5 minutes, 5 to 15 minutes, by shade) |
| Who heard you | Adds a hollow ring for each PSK Reporter receiver that heard me in the last 15 minutes. A station I hear that also hears me shows as a dot inside a ring |
| Day and night | World view only: shades the night side of the earth (greyline) |

A legend under the map says in words what each colour means. Labels are placed as in Reach (overlaps dropped, shown on hover); hover shows call, place, need, report and when heard. Without a grid the map falls back to the world view with "Set your grid in Settings to centre the map on you."

Below the map, **Band activity, last hour**: one bar per minute with the average decodes per slot (grey) and the average needed stations per slot (accent) drawn over it, then one sentence: "About 5 needed stations per slot. Busier than half an hour ago." (decodes per slot are not repeated: the band summary shows them, owner request 2026-10-12) (busier or quieter when the last 10 minutes differ from 30 to 40 minutes ago by 25% or more).

**Calling you** (formerly "Waiting for you"; owner decision 2026-10-11): a bar pinned above the top slot, outside the scrolling area, shown only when someone has called me and I have not answered. Each caller shows callsign, where, need tag, "Called you at HH:MM:SS · SNR", and a button "Answer after this contact" (or "Answer" when idle). Up to 5 callers, oldest first. A station whose contact I abandoned (or that ended with no reply or by the watchdog) and that replies within 5 minutes is also shown, whatever it sent, with "Replied after you stopped: <message>" and a "Resume" button. Each caller also has a dismiss "✕" (owner request 2026-11-09): it takes the caller off the bar without sending anything. A dismissed station stays off while it keeps calling; once it has not called for 2 minutes, its next call shows it again. Calling it from the list, or it answering my CQ, ends the dismissal. The phone's Calling you list has the same "✕". In the station list, the button of a station calling me reads "Answer" and answers it the same way. An optional chime (Settings, Operating, off by default) plays on the computer's default speakers when a new caller appears; it stays silent if the default device is the radio's audio device or no radio audio device is set.

**Your signal on 20 m**: see Reach, below. Compact form, with no map (owner request 2026-10-12: Heard here's "Who heard you" rings already map these stations): headline counts, the countries line under it ("USA 12 · Canada 3 · Japan 1", most stations first, at most six then "+4 more"; tooltip lists every country with best report and farthest station; owner request 2026-10-26), farthest, best report, the neighbour summary line when there is one ("7 dB weaker than stations near you into Europe."), the three newest reports (call, where, dB, age), link "Open reach view". Caption "PSK Reporter · live", or "· updated 6 min ago" on the query fallback, or "· unavailable".

**Worked this session**: title with count, subline "N new DXCC · N new band DXCC". The three newest contacts: call · where, upload status ("Uploaded to QRZ", "Waiting to upload · retrying", "Upload failed · Retry"), need tag at the time of the contact, time. Link "Show all N in the log".

**How the line is ordered**: one sentence and a "Change" link that opens the ranking settings flyout: tier order (drag to reorder), and checkboxes Prefer stations that hear me (on), Only stations calling CQ (on), Show Repeats (on; stations worked on this band are never in the line, this only hides the Repeats card), Skip long shots (off).

## Mini mode

A small strip, 480 px wide, that stays on top of other windows so the operator can use another program while Costas keeps watch. Opened from the menu or Ctrl+M; the main window hides while it is open. The expand button (⤢) or closing the strip brings the main window back. The strip remembers its position.

It mirrors the Operate view and holds no state of its own. Nothing transmits without a click.

| Row | Behaviour |
| --- | --- |
| Top | Band and mode ("20 m FT8"), the slot ("Receiving · 9 s", critical colour while transmitting), the count ("6 in line", or "2 calling you" in caution colour when anyone is calling you), and the expand button |
| Idle | The station at the top of Next in line: call, where and dB, and why it is worth calling in the need colour, with a **Call** button. "Nobody to call yet." when the line is empty, with no Call button |
| In a contact | The call, a small progress track with one dot per step (same colours as the conversation strip), and the message on the air or being waited for. No Call button |
| Halt | Always there, at the right of the second row. Critical colour while transmitting. Esc in the strip does what it does in the main window |

The strip is outlined in the on-air colour while transmitting, like the main window.

## Desktop notifications

Owner request 2026-11-18. Costas raises a notification in the operating system's notification centre (Windows toasts; `org.freedesktop.Notifications` on Linux) for each "needs you" event, while it is in the background: minimized, in mini mode, or another program active. The taskbar flash and the chimes are unchanged and decided by their own settings.

| Event | Title | Text |
| --- | --- | --- |
| Hunt list alert | "VP8PJ on 20 m FT8" | "New country Falkland Islands, heard here at -12 dB." For a country, state or grid entry the match comes first ("Alaska (KL), heard 300 mi from you at -14 dB."); for a call entry, "On your Hunt list, heard here at -12 dB." |
| A station calls me (new in Calling you) | "W1ABC is calling you" | "20 m FT8 · -9 dB · W7LIT W1ABC FN31" (band and mode, its dB at my station, the message it sent) |
| A station replies after I stopped its contact | "W1ABC replied after you stopped" | As for a caller |
| Several of one kind within a minute (merged) | "3 Hunt list stations heard" / "3 stations are calling you" | "VP8PJ on 20 m FT8, KL7RA on 40 m FT8, VK9XA on 17 m FT8." or "W1ABC, K2DEF, N3GHI." (4 named, then "and N more.") |

Rules (`NotificationGate`, Core):

- Off, or that kind of event off: nothing. "Only when Costas is in the background" on (default) and the main window active: nothing, and the event does not count as notified.
- A station notified for one kind stays quiet for that kind for 10 minutes.
- At most one notification of each kind pops up per minute. Further events of that kind in that minute replace it quietly, as the merged notification, without popping up again (Windows: same tag, `SuppressPopup`; Linux: `replaces_id`). After the minute the next event pops up fresh.
- Notifications are silent (the chime settings decide whether Costas makes a sound) and use the default urgency and scenario, so Windows Focus Assist and a desktop's Do Not Disturb hold them back as for any program. Costas does not work around them.
- There are no buttons. Clicking a notification brings Costas out of mini mode and to the front, on Operate, with the station selected in Next in line when it is there (a station heard on another band through PSK Reporter is not in the list; the Hunt bar still offers Go to band). **A notification never calls, transmits or changes frequency.**
- Where no notification centre can be used (no D-Bus session or notification server, toasts turned off for Costas, macOS for now), nothing is shown and the Notifications page says so.

## Raw decodes view

The classic list for operators who want it. Columns: UTC, local time, dB, DT, Hz, message, country, distance, why. Newest slot at the top (owner request, replacing newest at the bottom), blank line between slots, keeps scroll position when scrolled down away from the newest lines, with a "Jump to live" button. Messages to me in critical colour, CQ in accent. Double-click calls the sender. A filter box matches callsign, message text or country.

Owner request 2026-10-26 ("more feature rich"):

- **Distance** and **Why** come from the station list (the same text and colours as its Why column); blank for my own transmissions and for senders the list does not know.
- **Show** dropdown beside the filter, as in the Log view: All lines (default), CQs, To or from me (messages to me and my own transmissions), New DXCC, band DXCC or grid. While the filter or Show hides lines, "5 of 212 lines" and a "Clear filters" link appear.
- Lines from the contact partner, and my transmissions to them, get the in-contact tint and left bar used on Operate.
- Right-click a line: Call <call>, Show only <call> (sets the filter), Copy line (`HHMMSS  dB  DT  Hz ~  message`, as WSJT-X prints it).

## Reach view

Mockup: `reach`. Band dropdown and time-window dropdown (15 minutes, 1 hour, 24 hours).

Layout (owner request): the map card with the plot and the by-band stats fills the top two thirds of the view; Who heard you fills the bottom third and scrolls on its own, with its column headings fixed.

- **Headline**: heard by N stations, countries, farthest distance, best report.
- **Plot**: azimuthal equidistant, centred on my grid, north up, rings every 2,000 miles (or 3,000 km), scale to the farthest report rounded up to the next ring. One dot per reporting station at its bearing and distance. Three strength classes by size and shade: strong (-10 or better), fair (-11 to -17), weak (-18 or below). Label dots with callsigns, with collision avoidance: drop labels that would overlap, show them on hover. This fixes the overlap visible in the mockup. When my grid is known, land and country borders (Natural Earth 1:110m, public domain, bundled) are drawn faintly behind the rings in the same projection, on both the full plot and the compact one.
- **Zoom**: the full plot zooms up to 16×. Mouse wheel zooms about the pointer; +, − and "Show the whole map" buttons sit beside the plot with the zoom factor under them. Drag to move around when zoomed; double-click shows the whole map. The frame, the N marker and line widths stay fixed; the map, rings, dots and labels scale, so more labels fit as you zoom in. The compact plot on Operate does not zoom.
- **Who heard you**: grid of station, where, miles, bearing, dB, age, "In your log" (Needed in accent, or Worked). Sortable; default by distance. Double-click a row opens that station's details.
- **Right column tabs** (owner request 2026-11-01: the single long scroll hid its lower sections): Activity (Band activity near you), Countries (By country), Bands (By band), Nearby (Compared with stations near you), Changes (Did a change help?). Each tab scrolls on its own; the map tips sit under the tabs. Opens on Activity.
- **Band activity near you** (Activity tab; owner request 2026-11-01): a compact grid with one row per band that has spots: band, one cell per continent (NA, SA, EU, AF, AS, OC) holding its spot count ("140", "1.2k") and shaded as the band menu chips, the total spots ("~1,240" when estimated), and the needed countries ("Brazil +2" in caution colour, tooltip lists all). The radio's band is highlighted. Bands with no spots get no row; one line under the grid names them ("No spots on 160 m, 80 m and 60 m."). A band waiting for its sampling turn keeps its row with the reason ("Checking soon") across the cells.
- **By country, last <window>** (owner request 2026-10-26; above By band): country, stations, best report, farthest, for the selected band and window. Most stations first, then farthest. Stations with no known country are left out, as in the headline count. Row tooltip names the best and farthest stations.
- **By band, last 24 hours**: band, stations that heard you, farthest, median report. Current band highlighted.
- **Compared with stations near you** (under By band; `04-domain-logic.md` section 13): the summary line, "Compared with 9 stations within 300 miles, last 30 minutes.", and a row per continent: toward, my median, theirs, shared receivers, difference ("7 dB weaker" in caution colour, "Level", "3 dB stronger"). It covers the band and mode I am on: for another band it says "Shown for the band you are on, 20 m."; with too few shared receivers, "Not enough shared reports yet. It needs 3 receivers on one continent that heard both you and a station near you."; with the setting off, "Turned off in Settings, PSK Reporter."
- Empty state: "No reports yet. Reports appear a few minutes after you transmit."

## Log view

Grid of all contacts, newest first: date, time, call, band, mode, sent, received, grid, country, name, need tag at the time, QRZ status. Search box. Edit and delete (delete asks once; QRZ deletion is a separate explicit action). Buttons: Import ADIF, Export ADIF (writes the contacts the filters show, including a custom UTC date range; options for plain ASCII text and leaving out name, QTH and comment), Sync with QRZ now. Status line: "2,418 contacts · last synced 02:50 UTC".

## Awards view

Owner request 2026-10-13. Left column: a card each for DXCC (any band), WAS (50 US states; Alaska and Hawaii count though they are separate DXCC entities), WAZ (40 CQ zones, from the country file's zone for each call's prefix) and VUCC on 6 m (4-character grids). Each card shows worked and confirmed counts, a bar towards the award (confirmed in accent, worked faintly behind), the goal ("100 confirmed for the award · 340 in all") and, for WAS and WAZ, "Still needed" and "Worked, not confirmed yet" lists. Below, DXCC by band: every band with contacts and always 80, 40, 20, 15 and 10 m, with the 5-band DXCC line ("2 of 5 bands have 100 countries confirmed"). Right column: Countries still needed, for any band or one band, optionally with those worked but not confirmed; each row has the country, continent and status, and "when to look" from the operator's own decode journal ("Heard 14 times, mostly on 20 m, usually 13:00–15:00 UTC, last heard 3 Oct.", or "Not heard in your decodes yet."). Countries heard most often come first. Confirmed means the log's confirmed flag. Counting is `Core/Awards/AwardBuilder`; the sentence is `Core/History/WhenToLook`. A "time" is a distinct band and UTC hour. The journal is read in the background at start-up and again when the view or a station's details open and the copy is over 10 minutes old. The station details strip shows the same sentence for the station's country ("Mongolia: heard 6 times, …") when there is history. Empty log: "Your log is empty. Sync with QRZ or import an ADIF file on the Log view, and your award progress appears here."

**Spotted on the DX cluster** (item C4, a card in the left column above DXpeditions on the air): needed stations the DX cluster spotted in the last 30 minutes in the mode being worked (`05-integrations.md` section 5b, `Core/Cluster/ClusterNeeds`): new DXCC (caution) or new DXCC on the spot's band (accent), new countries first, then newest, at most 12. Each row: call, country, "21074.0 · Spotted 4 min ago on 15 m by DL1ABC · FT8 -12dB", the need, and for a spot on another band a **Go to 15 m** button (tooltip "Changes band only. Nothing is sent and nobody is called."). Go to uses the Hunt alert's band change: refused with a status line while transmitting, only to a band in the band list, to its dial frequency. Nothing else in the list acts. The summary line says how many there are and that spots never change who is called; with the cluster off it says "DX cluster spots are off. Settings > Who to call > DX cluster turns them on."; with none, "No needed FT8 stations spotted in the last 30 minutes." The list follows band and mode changes and refreshes its ages every 30 s while shown.

## First-run setup

Mockup: `setup` (step 2). A left rail lists the six steps; the right pane holds the current one; Back and Next at the bottom right. Every step can be revisited from Settings.

| Step | Content | Passes when |
| --- | --- | --- |
| 1 Station | Callsign, grid (4 or 6 characters), distance units | Callsign and grid are valid |
| 2 Radio | Model (searchable list from Hamlib), port (auto-scan), baud (auto), PTT method. "Test connection" lists a ✓ or ✗ line for each check (rigctld found, port present and opens, radio answers with dial and mode) with what to fix, and never keys the radio. Link "No radio control? Use audio and VOX only" | Frequency is read back, or audio-only is chosen |
| 3 Audio | Input and output device by name, live input level meter with a "good" band (-40 to -10 dBFS) that names no audio, silence, too quiet, too loud and clipping. "Check output" plays one second of silence to the output and confirms the device played it | Input level is in range for 5 seconds |
| 4 Transmit test | Explains what will happen, asks for a dummy load or low power, a "Send test tone" button (2 seconds). After the tone, the ALC and SWR read during it, with any warning | Operator confirms the radio transmitted. Skippable |
| 5 Clock check | Measured offset against internet time, pass under 0.5 s, link to Windows time settings | Offset measured. Skippable when offline |
| 6 QRZ | Logbook API key, optional QRZ username and password for lookups, "Test" buttons, and a line saying why each is needed (`05-integrations.md` section 4). Then first sync with a progress count | Skippable |

## Settings

Sections: Station profiles (each with its callsign, grid, power and **US licence class**: Amateur Extra, Advanced, General, Technician or Not US / don't check, owner request 2026-11-18; off by default: a new install's profile is Not US until a class is chosen, owner decision 2026-11-18, while a profile from an earlier build with none chosen follows that build's "US data segments only" switch, normally Extra; the transmit guard keeps to the class's data segments, `04-domain-logic.md` section 19, and a refusal says "Not sent: your General licence allows data on 20 m only on 14.025-14.150 MHz, and this signal would take …" or, for a band with none, "… doesn't allow data on 20 m, so the band is receive only for you"; the band menu marks such bands "Receive only") · Radio · Audio · Transmit (power, watchdog minutes, late start for a click: how far into my slot a Call or CQ I click may still go out in that slot, leaving out the part already past, 0 to 5 s, default 3, PR #150, owner sign-off 2026-11-14; double-click switches stations during a contact, on or off, default off, PR #150, owner sign-off 2026-11-14; retry limit, RR73 or RRR, CQ modifier, my grid in my answer to a CQ on or off, `73` or `RR73` as my last message when answering, optional free text up to 13 characters sent after it, a preview of every message as it will be sent, auto-log on or off) · Ranking (also how long CQ callers stay in Next in line after they were last heard: only while heard every slot, or 1 to 15 minutes, default 2) · QRZ · Logbooks › Backups (no settings: shows the backups folder with an **Open folder** button; the ADIF log copy's file names, one per station callsign, and "Last written 18 Nov 2026 21:04.", or on a failure "Could not write the copy: <reason>. Trying again at 21:10." in caution colour; and the newest daily copy, or "Daily copies of the database are not taken in simulation."; a failed write also adds a **Log copy** light to the bottom bar until a write works) · PSK Reporter (receive feed on or off, upload my spots on or off) · Notifications (desktop notifications on or off, default on; for Hunt list alerts and for a station calling me, each default on; only when Costas is in the background, default on; see Desktop notifications) · DX cluster, in Who to call (on or off, off by default; the node's host and port, default 7300; a drop-down of well-known public nodes that fills them in, none endorsed) · Interop (UDP address and port) · Files (WAV saving: none, decoded, all; retention days) · Mobile connection (on or off, applied at once; the address to open on the phone, shown large while it is on; no pairing code, owner decision 2026-10-25; port) · Appearance (skin, theme, units).

## Phone (remote page)

A single page served by the app to any phone on the home network (no pairing code since 2026-10-25) (owner decision 2026-10-12). Top: my call, frequency, band and mode, link state, radio dot, UTC, clock and decoder, and who has control ("You control", "Desk controls", "Another phone controls"). Under the top bar, notices that someone else signed in or took control (owner request 2026-11-17): every phone but the one it is about sees them for ten minutes or until dismissed, and the desk's remote indicator shows the latest one about a phone for a minute. They inform only; nothing needs approving. Below them, on yellow, a signed-in user linked to a station profile other than the one in use reads "You're operating as K1UTA (Ash profile)." with an "Operate as K1GLV" button, greyed out with the reason while the station is busy; the switch happens only on that tap (owner request 2026-11-18). In Settings › Mobile connection each user's card has "Operates as": "The profile in use" or one of the station profiles, shown as "Name (CALL)"; it takes effect only with sign-in on (D25). An ON AIR line with the message while transmitting. A yellow banner for the fault line (for example "Transmit stopped at 20:31:15: lost touch with the phone."). Under it, the desk's "Why it's quiet" line with the same wording, and its Go to button when the fix is a frequency (owner request 2026-11-18); the station tunes to the frequency in its own readout, never one the phone sends, and refuses while transmitting. The contact card with the same wording and five-step strip as the desk, the messages around the current step (tap an upcoming one of mine to jump), and Resend, Log now and Abandon; when idle, Call CQ or Stop CQ. Calling you and Next in line (up to 8) with their buttons. HALT TX pinned to the bottom in portrait, and at the right of the sticky top bar at 760 px wide or more (phone landscape, tablet, browser), grey when nothing is going out (owner decision 2026-11-25). The desk's bottom bar shows "Phone in control" or "Phone connected".

Richer page (owner request 2026-10-12, "more feature rich"): under the top bar, a slot countdown bar (red while transmitting), the Tx meter warning after a transmission outside limits, and one line when QRZ, PSK Reporter or audio is not working. Five tabs: Operate (contact card, Calling you, Next in line with the desk's columns: doing now, where, signal, hears you, chance, last heard, last worked), Stations (band summary figures, Repeats up to 8, Watching up to 8 with the reason), Decodes (band activity, newest first, up to 60 lines, coloured as on the desk), Signal (the desk's "Your signal" panel: headline, farthest, best, nearby stations, latest reports), Log (this session's contacts). Tapping a call opens a station sheet with the content of the desk's station details and a Call or Answer button when the station can be called. Tapping the frequency opens a band sheet: mode, band (not while transmitting; confirms before ending a contact or CQ) and Tx offset (Automatic, or 200 to 2900 Hz), with the desk's wording and saved to settings. Opening details does not take control; band and offset changes do. The tab in use is remembered on the phone. Phone Operate redesign (owner pick 2026-11-01, "Widgets" variant): on a phone the Operate tab is a grid of tiles instead of the desk's layout: Contact (state, slot seconds, call, need, place, message on the air or awaited, reports, labelled five-part progress, buttons; red while transmitting), Calling you, Hearing you (15-minute count, last-hour bars, farthest), Band now (heard, countries, calling CQ, you need, logged), Next in line as a compact table (call, dB, why, pile count, Call; sort Best first / Strongest / Call, remembered) and Logged this session. Tiles for Hearing you, Band now and Logged open their tabs. The Band now tile and the Stations tab's band summary add the desk's band advice open line (the Stations tab also the closing line), with the same wording; no tooltip. The slot bar and ON AIR banner are hidden while the tiles show, since the Contact tile carries both. Tablets keep the desk-like layout.

Tablet layout (owner request 2026-10-12): at 760 px wide and up the same page shows the desk's Operate view at once, with two tabs (Operate, Decodes). Next in line and Repeats are tables with the desk's columns (Call with need tag, Where, Doing now, Signal, Chance with its reason, Last heard, Last worked, button); Watching shows Call, Where and why not now. Portrait (760 to 1079 px) is one column and drops Last worked; landscape (1080 px and up) adds a 340 px right column with the band summary, your signal and this session's contacts. The contact card shows all five messages under the strip. Touch targets are at least 44 px; station details open as a right-hand panel; HALT TX is a 420 px button pinned bottom right.

Home-screen app (owner request 2026-10-20, "make it a PWA"): the page links a web app manifest (`display: standalone`, Costas icon) and the Apple home-screen tags, so Add to Home Screen gives an icon that opens full screen on iOS and iPadOS. There is no service worker and no install prompt, because both need a secure (HTTPS) origin and the page is plain HTTP on the LAN. Chrome on Android adds a shortcut that opens in a tab. The top bar and the tablet side panel pad for the status bar and notch.


Audio dropouts, decode time per slot, median DT of decodes, clock offset, CAT round-trip time, ALC and SWR of the last transmission, `jt9` and `rigctld` process state and restarts, QRZ and PSK Reporter connection state and last error, upload queue length. A button "Save diagnostics bundle" writes a zip of logs and settings (with secrets removed).

## Keyboard

| Key | Action |
| --- | --- |
| Enter | Call the selected station; in a contact, nothing |
| Up, Down | Move selection in the line |
| Esc | Halt Tx. In a contact: abandon it. Once both reports are exchanged, the first press stops transmitting but keeps the contact (a late RR73 is still logged) for the 4 slots after the Esc's own, then it closes as abandoned (owner decision 2026-11-24); the second press, or starting another contact, abandons at once (owner decision 2026-10-20) |
| Ctrl+Q | Call CQ / stop CQ |
| Ctrl+R | Resend |
| Ctrl+L | Log now |
| Ctrl+1 to Ctrl+5 (Ctrl+6 during a contest) | Switch view |
| Ctrl+M | Mini mode |
| F1 | Keyboard shortcuts |

## States every data-bound element must handle

Loading (skeleton or "—", never a spinner over the whole view), empty, stale (secondary text with age), and error (inline text with a retry action). No modal dialogs during operating except the confirm for Abandon after reports are exchanged.

## Accessibility

Every control has an accessible name. The line and grids are keyboard navigable. Contrast at least 4.5:1 for text. Tx state is announced to screen readers when it starts and stops.

## Contest mode (owner decision 2026-11-07)

- **Picker**: the CQ modifier menu beside Call CQ gains a "Contest (changes the exchange)" heading and four contests, each with its CQ (`CQ RU`). Picking one opens the exchange sheet when no usable exchange is saved; otherwise the contest starts at once.
- **Exchange sheet**: the contest's name, one box (state or province, or DX; or class and section; nothing for the grid contests), a note, and a preview of every message the contest sends with "‹call›" in place of the other station. Start contest is enabled only when every message encodes exactly.
- **Contest bar**: an amber bar across the top of Operate while a contest runs: CONTEST badge, name, what I am sending, contacts, multipliers (when the contest has them), dupes, and Change exchange / End contest.
- **Contact card**: during a contest contact the reports read "You sent" and "They sent" and show the exchanges, with no dB unit.
- **Next in line**: Why shows "New mult · X" in green or "Dupe" in grey for contest stations.
- **Contest view**: tiles for contacts, multipliers, claimed score and rate; a per-band table; the multiplier grid (states and provinces for the Roundup, fields for WW Digi) with worked ones in green; the contest log, newest first, with New mult and Dupe tags; a dupe check box that filters the log and says whether the call is a dupe on the current band; Export Cabrillo….

