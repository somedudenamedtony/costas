# 02 · UI spec

The mockups in `mockups/` are the visual reference. This document says what each part is, where its data comes from and how it behaves. Where they disagree, this document wins.

## Look

Native Windows 11. Use Avalonia's Fluent theme and stock controls; do not restyle controls beyond the tokens below. Follow the system light or dark theme and the system accent colour. The values below are the light theme with the default accent, for reference.

| Token | Value | Use |
| --- | --- | --- |
| Window background | `#F3F3F3` | Behind cards |
| Card background | `#FFFFFF`, border `#E0E0E0`, radius 8 | Each section |
| Control | height 32, radius 4, border `#D1D1D1` | Buttons, dropdowns |
| Text | `#1A1A1A` | Primary |
| Secondary text | `#5F5F5F` | Labels, captions |
| Accent | system accent (`#005FB8`) | Selected state, primary button, "new band", "new grid" |
| Caution | `#8A4B00` | "New country", upload waiting |
| Critical | `#C42B1C` | Transmitting, Abandon, Halt while transmitting |
| Success | `#0F7B0F` | Connected indicators |
| Selected row | `#E5F1FB` with a 3 px accent bar on the left | Top of the line, selected station |
| Font | Segoe UI Variable, 13 px body, 15 px section titles, 40 px current callsign | |
| Message font | Cascadia Mono, 12 px | Only for literal FT8 message text |
| Numbers | Tabular figures in every column of numbers | |

Colour is never the only signal: every state also has text.

## Window

One window, minimum 1100 × 720, default 1440 × 900, remembers size and position. Layout from top to bottom: top bar, main area, bottom bar. The main area has a wide left column and a right column of about 300 px. Below 1100 px wide the right column drops under the left.

### Top bar

| Element | Behaviour |
| --- | --- |
| Band dropdown | Lists bands from the frequency table. Changing it sets the dial frequency through the rig service, clears the station list for the old band and loads log totals for the new one. Disabled while transmitting |
| Mode dropdown | FT8, FT4. Changes slot length and dial frequency. Disabled while transmitting |
| Frequency | Dial frequency read from the radio, `14.074.000` format, MHz label. Read-only in v1 |
| View tabs | Operate, Raw decodes, Reach, Log. Ctrl+1 to Ctrl+4 |
| Clock | UTC `HH:MM:SS`. Turns caution colour with a tooltip when the clock offset exceeds 0.5 s; critical above 2 s |
| Menu button | Settings, Station profile, Diagnostics, About |

### Bottom bar

| Element | Behaviour |
| --- | --- |
| Status text | One plain line: "Not in a contact. Enter calls ZL2RPA, the top of the line." / "Transmitting to ZL2RPA." / "Waiting for ZL2RPA." / fault text in critical colour |
| Tx offset | "Tx offset 1650 Hz · clear". Click opens a small flyout: automatic (default) or a manual value |
| Call CQ | Starts calling CQ in the next slot of the chosen parity. Becomes "Stop CQ" while running |
| Halt Tx | Always enabled. Drops PTT immediately and stops sequencing. Critical colour while transmitting. Esc does the same |
| Slot progress | Label "Receiving" or "Transmitting", seconds to next slot, a 4 px bar. Critical colour while transmitting |
| Indicators | Radio, audio, QRZ, PSK Reporter: a dot and a word each. Click opens Diagnostics |

## Operate view

### Top slot, idle: band summary

Mockup: `main-idle`. Shown when no contact is in progress. Three groups in one card.

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
| Tonight | Contacts this session · new countries · new bands. A session starts at app launch or after 2 hours with no contact |

### Top slot, in a contact

Mockup: `main-in-contact`. Replaces the band summary from the moment the operator calls a station or answers a caller, until the contact is logged or abandoned. After logging it stays for one slot showing "Logged", then returns to the summary.

| Element | Content |
| --- | --- |
| Caption | "Contact in progress", or "Calling", "Logged", "No reply" |
| Callsign | 40 px, with the need tag beside it |
| Subline | Name from QRZ lookup (blank until it arrives) · country or state · distance |
| You sent / You received | The reports, blank until exchanged |
| Resend | Sends the current step again in my next slot |
| Log now | Logs the contact as it stands. Enabled once both reports are exchanged |
| Abandon (Esc) | Stops transmitting, ends the contact, logs nothing |

**Conversation strip**: five boxes in a row, mine raised 12 px, theirs lowered 12 px.

| Box state | Look | Content |
| --- | --- | --- |
| Done | Accent tint, solid border | "You · sent 02:54:00" or "ZL2RPA · received 02:54:15", and the literal message |
| Now, transmitting | Critical fill, white text | "You · sending now, 7 s left" and the message |
| Now, waiting | Accent border, no fill | "ZL2RPA · expected 02:54:45" and the expected message |
| Upcoming | Dashed border, secondary text | The expected message |
| Repeated | Done look plus "×2" | When a step was sent or received more than once |

Clicking one of my upcoming boxes jumps the sequence to that step (manual override). The boxes are generated from the contact state machine's step list, so the CQ-run flow shows its own six steps.

### Next in line

A grid with a header row. One row per station that is needed (tiers 1 to 4), callable (see `04-domain-logic.md`) and passes the filters. Sorted by the ranking rule. At most 12 rows; the rest are counted in a footer link "and N more".

| Column | Content |
| --- | --- |
| # | Rank, starting at 1 |
| Call | Callsign, semibold |
| Where | Country, or "State, USA" / "Province, Canada" when known, then distance |
| Why | Need tag: New country (caution, bold), New band (accent, bold), New grid (accent, bold), New call (plain) |
| Doing now | "Calling CQ, 6 slots" · "Calling CQ DX, 2 slots" · "Calling CQ, 4 slots, fading" · "Calling you" |
| Hears you | "Yes, -22" (direct report in the last 15 min) · "Japan does, at -17" (best report from the same entity) · "No reports yet" |
| dB | Their latest SNR at my station |
| Chance | Good (bold), Fair, Long shot |
| Call | Button. Calls that station |

Behaviour:

- Row 1 is highlighted as the selected row when nothing else is selected. Up and Down move the selection. Enter calls the selected row. Double-click calls that row.
- Rows re-rank only at slot boundaries, after decodes arrive, and never while the pointer is over the list or within 300 ms of a click. A pending re-rank shows a "List updated" pill that applies on click or when the pointer leaves.
- Selecting a row loads its QRZ lookup and shows a details flyout to the right of the row: name, location, grid, bearing, and the operator's past contacts with that call (date, band, mode, confirmed).
- Empty state: "Nobody you need is calling CQ right now." with the Watching list still shown.

### Watching

Stations that are needed but not callable now. Columns: Call, Where, Why, reason. Reasons: "Went quiet 45 seconds ago", "In a contact with EA5HVK", "Finishing a contact with K7RGB". Rows are secondary text. Maximum 8 rows.

Footer: "N stations you have already worked on 20 m are hidden. Show them" toggles a third section, "Already worked", with the same columns as Next in line.

### Right column

**Waiting for you** (only when someone has called me and I have not answered): callsign, where, need tag, "Called you at HH:MM:SS · SNR", and a button "Answer after this contact" (or "Answer" when idle). Up to 5 callers, oldest first.

**Your signal on 20 m**: see Reach, below. Compact form: mini plot 124 px, headline counts, farthest, best report, the three newest reports (call, where, dB, age), link "Open reach view". Caption "PSK Reporter · live", or "· updated 6 min ago" on the query fallback, or "· unavailable".

**Worked tonight**: title with count, subline "N new country · N new bands". The three newest contacts: call · where, upload status ("Uploaded to QRZ", "Waiting to upload · retrying", "Upload failed · Retry"), need tag at the time of the contact, time. Link "Show all N in the log".

**How the line is ordered**: one sentence and a "Change" link that opens the ranking settings flyout: tier order (drag to reorder), and checkboxes Prefer stations that hear me (on), Only stations calling CQ (on), Hide stations worked on this band (on), Skip long shots (off).

## Raw decodes view

The classic list for operators who want it. Columns: UTC, dB, DT, Hz, message, country. Newest slot at the bottom, blank line between slots, keeps scroll position when scrolled up with a "Jump to live" button. Messages to me in critical colour, CQ in accent. Double-click calls the sender. A filter box matches callsign or text.

## Reach view

Mockup: `reach`. Band dropdown and time-window dropdown (15 minutes, 1 hour, 24 hours).

- **Headline**: heard by N stations, countries, farthest distance, best report.
- **Plot**: azimuthal equidistant, centred on my grid, north up, rings every 2,000 miles (or 3,000 km), scale to the farthest report rounded up to the next ring. One dot per reporting station at its bearing and distance. Three strength classes by size and shade: strong (-10 or better), fair (-11 to -17), weak (-18 or below). Label dots with callsigns, with collision avoidance: drop labels that would overlap, show them on hover. This fixes the overlap visible in the mockup.
- **Who heard you**: grid of station, where, miles, bearing, dB, age, "In your log" (Needed in accent, or Worked). Sortable; default by distance. Double-click a row opens that station's details.
- **By band, last 24 hours**: band, stations that heard you, farthest, median report. Current band highlighted.
- Empty state: "No reports yet. Reports appear a few minutes after you transmit."

## Log view

Grid of all contacts, newest first: date, time, call, band, mode, sent, received, grid, country, name, need tag at the time, QRZ status. Search box. Edit and delete (delete asks once; QRZ deletion is a separate explicit action). Buttons: Import ADIF, Export ADIF, Sync with QRZ now. Status line: "2,418 contacts · last synced 02:50 UTC".

## First-run setup

Mockup: `setup` (step 2). A left rail lists the six steps; the right pane holds the current one; Back and Next at the bottom right. Every step can be revisited from Settings.

| Step | Content | Passes when |
| --- | --- | --- |
| 1 Station | Callsign, grid (4 or 6 characters), distance units | Callsign and grid are valid |
| 2 Radio | Model (searchable list from Hamlib), port (auto-scan), baud (auto), PTT method. Live frequency readback. Link "No radio control? Use audio and VOX only" | Frequency is read back, or audio-only is chosen |
| 3 Audio | Input and output device by name, live input level meter with a "good" band | Input level is in range for 5 seconds |
| 4 Transmit test | Explains what will happen, asks for a dummy load or low power, a "Send test tone" button (2 seconds) | Operator confirms the radio transmitted. Skippable |
| 5 Clock check | Measured offset against internet time, pass under 0.5 s, link to Windows time settings | Offset measured. Skippable when offline |
| 6 QRZ | Logbook API key, optional QRZ username and password for lookups, "Test" buttons, and a line saying why each is needed (`05-integrations.md` section 4). Then first sync with a progress count | Skippable |

## Settings

Sections: Station profiles · Radio · Audio · Transmit (power, watchdog minutes, retry limit, RR73 or RRR, auto-log on or off) · Ranking · QRZ · PSK Reporter (receive feed on or off, upload my spots on or off) · Interop (UDP address and port) · Files (WAV saving: none, decoded, all; retention days) · Appearance (theme, units).

## Diagnostics

Audio dropouts, decode time per slot, median DT of decodes, clock offset, CAT round-trip time, `jt9` and `rigctld` process state and restarts, QRZ and PSK Reporter connection state and last error, upload queue length. A button "Save diagnostics bundle" writes a zip of logs and settings (with secrets removed).

## Keyboard

| Key | Action |
| --- | --- |
| Enter | Call the selected station; in a contact, nothing |
| Up, Down | Move selection in the line |
| Esc | Halt Tx. In a contact: abandon it (second press confirms if both reports are exchanged) |
| Ctrl+Q | Call CQ / stop CQ |
| Ctrl+R | Resend |
| Ctrl+L | Log now |
| Ctrl+1 to Ctrl+4 | Switch view |
| F1 | Keyboard shortcuts |

## States every data-bound element must handle

Loading (skeleton or "—", never a spinner over the whole view), empty, stale (secondary text with age), and error (inline text with a retry action). No modal dialogs during operating except the confirm for Abandon after reports are exchanged.

## Accessibility

Every control has an accessible name. The line and grids are keyboard navigable. Contrast at least 4.5:1 for text. Tx state is announced to screen readers when it starts and stops.
