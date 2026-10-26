# 02 · UI spec

The mockups in `mockups/` are the visual reference. This document says what each part is, where its data comes from and how it behaves. Where they disagree, this document wins.

## Look

Native Windows 11. Use Avalonia's Fluent theme and stock controls. Classic, the default skin, follows the system light or dark theme and the system accent colour. Settings › Appearance also offers Ocean (cool blue), Forest (soft green), and Amber (warm sand and copper) skins, each with light and dark palettes. The theme choice (system, light or dark) is independent of the skin. Light and dark swatches preview the skin; Save applies it to all desktop windows without a restart. Modern Radio, Classic Transceiver, Vintage Receiver and SDR Console additionally provide distinct faceplates, panel geometry, typography and control styling. Classic Transceiver uses gray and black surfaces and a vector seven-segment lime green LCD readout, including dim inactive segments; the frequency stays available in its button's accessible name. Their large frequency displays and dial buttons open the existing dial editor, obeying the same transmit lockout. Segmented slot strips and the vintage needle display the real slot progress, never simulated signal strength. Instrument faceplates replace the small top-bar frequency readout and fit the minimum 1100 × 720 window. Switching back to a palette skin removes the instrument styles and restores the standard readout. Skins change surface, text, accent and selection tokens through Fluent palettes; transmit, caution and success colors retain their meanings. The values below are Classic's light theme with the default accent, for reference.

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

One window, minimum 1100 × 720, default 1440 × 900, remembers size and position. Layout from top to bottom: top bar, main area, bottom bar. The main area has a wide left column and a right column of about 300 px, widening to 460 px when the window is 1600 px or wider so the map has room. Below 1100 px wide the right column drops under the left.

### Top bar

| Element | Behaviour |
| --- | --- |
| Band dropdown | Lists bands from the frequency table. Changing it sets the dial frequency through the rig service, clears the station list for the old band and loads log totals for the new one. Disabled while transmitting. The closed box shows the band only. The open list adds, per band, where stations near me are heard right now (`04-domain-logic.md` section 14): an activity bar and "1,240 spots" (or "No spots"), six continent chips NA SA EU AF AS OC shaded by spots, and "You need Brazil +2" in caution colour. Tooltips give the source and the full list of needed countries. With no reports (feed or setting off, nothing received yet) the list shows band names only. A band whose reports are not being received says why instead of "No spots": "Set your grid in Settings" when my configured grid is missing or invalid, "Checking soon" for a band not yet sampled after the feed fell back to the current band; sampled bands show estimates as "~1,200 spots" |
| Mode dropdown | FT8, FT4. Changes slot length and dial frequency. Disabled while transmitting |
| Frequency | Dial frequency read from the radio, `14.074.000` format, MHz label. A click opens a menu (product review 2026-10-12): a box to type a frequency ("14.090", "14090" kHz or "14.090.000") with Tune, "Your frequencies" (saved dial frequencies as buttons, each with Remove) and "Save the current frequency". Tuning changes band when the frequency is on another band; frequencies outside the band plan or on a band without transmit limits are refused with a sentence. Disabled while transmitting |
| View tabs | Operate, Raw decodes, Reach, Log, Awards. Ctrl+1 to Ctrl+5 |
| Clock | UTC `HH:MM:SS`. Turns caution colour with a tooltip when the clock offset exceeds 0.5 s; critical above 2 s |
| Menu button | Mini mode (Ctrl+M), Settings, Station profile, Diagnostics, Keyboard shortcuts, About |

### Bottom bar

| Element | Behaviour |
| --- | --- |
| Status text | One plain line: "Not in a contact. Enter calls ZL2RPA, the top of the line." / "Transmitting to ZL2RPA." / "Waiting for ZL2RPA." / fault text in critical colour |
| Tx offset | "Tx offset 1650 Hz · clear". Click opens a small flyout: automatic (default) or a manual value |
| Call CQ | Starts calling CQ in the next slot of the chosen parity. Becomes "Stop CQ" while running. Shown only on views other than Operate; on Operate the contact card's on-air panel has it |
| Tune | Owner request 2026-10-12: a toggle with a 10 s limit that starts the radio's own tuner when it has one. First it sends Hamlib's TUNE operation (`G TUNE`) after the radio and band-edge guards; if the radio accepts, the button reads "Stop tune" and the status line "The radio's antenna tuner is tuning." for 10 s (the radio runs its own cycle; Stop tune or Halt sends PTT off). If the radio has no TUNE operation or refuses it, a steady tone at the Tx offset (centred on where an FT8 signal sits) for up to 10 s (the Tune watchdog), PTT on at once, after the same radio, audio and band-edge guards as any transmission. Reads "Stop tune" while the tone is on. Disabled while CQ, a contact or another transmission is under way; a transmission the operator starts meanwhile replaces the tone |
| Halt Tx | Always enabled. Drops PTT immediately and stops sequencing. Critical colour while transmitting. Esc does the same |
| Slot progress | Label "Receiving" or "Transmitting", seconds to next slot, a 4 px bar. Critical colour while transmitting. Shown only on views other than Operate; on Operate the contact card's on-air panel has it |
| Meter warning | Only when the last transmission's ALC or SWR crossed a threshold (`04-domain-logic.md` section 11): "SWR 3.4 · ALC 70%", caution colour, critical when SWR is 3 or more. The tooltip and screen reader text give the full advice. Hidden when the meters read within limits |
| Indicators | Radio, audio, QRZ, PSK Reporter: a dot and a word each. Click opens Diagnostics |

## Operate view

### Top slot, idle: band summary

Mockup: `main-idle`. Shown below the contact card when no contact is in progress. Three groups in one card.

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

### Top slot, contact card

Mockup: `main-in-contact`. Always the first card on the Operate view, so the on-air panel (countdown and Call CQ) is always in the same place. From the moment the operator calls a station or answers a caller until the contact is logged or abandoned, it shows the contact and the band summary is hidden. After logging it stays for one slot showing "Logged", then goes idle.

Idle (no contact): caption "Not in a contact" (or "Calling CQ" with the subline "Waiting for a station to answer." while CQ is running), "—" in place of the call, the subline "Call a station in Next in line, or call CQ.", reports "--", no conversation strip, and Resend, Log now and Abandon disabled. The band summary shows below it.

| Element | Content |
| --- | --- |
| Caption | "Contact in progress", or "Calling", "Logged", "No reply", "In another contact" (the subline then gives the reason: "N8WCR answered K1ABC instead. Stopped calling." or "N8WCR is in a contact with K1ABC. Not calling.") |
| Callsign | 40 px, with the need tag beside it |
| Subline | Name from QRZ lookup (blank until it arrives) · country or state · distance |
| On-air panel | A bordered panel on the right of the card that holds everything about the next transmission, described below |

**On-air panel**: one row of readouts above one row of buttons.

| Element | Content |
| --- | --- |
| You sent / You received | The reports with "dB" after them. Shows "--" until exchanged |
| Slot | "Transmitting" or "Receiving", the seconds left, and a progress bar (critical while transmitting). While transmitting it counts down to the end of the transmission, otherwise to the next slot |
| Call CQ / Stop CQ | The same command as Ctrl+Q and the bottom bar's Call CQ button (shown on other views) |
| CQ modifier | Owner request 2026-10-12. A dropdown beside Call CQ: CQ, CQ DX, CQ NA, CQ SA, CQ EU, CQ AS, CQ AF, CQ OC, CQ POTA, plus the Settings value if it is another one. Picking saves it as Settings › Transmitting › CQ modifier, so both always agree; it applies from the next CQ sent |
| Resend | Sends the current step again in my next slot |
| Log now | Logs the contact as it stands. Enabled once both reports are exchanged |
| Abandon (Esc) | Stops transmitting, ends the contact, logs nothing |

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

**Contact station details** (beneath the horizontal conversation in the contact card): a shaded strip groups the station name, licence and QRZ/photo links; location, distance, county and local time; grid, bearing and CQ/ITU zones; and QSL methods and most-wanted rank. Values are emphasized above secondary details, and groups wrap on narrower windows. Missing optional facts are omitted, including the QSL/awards group when empty. Lookup status and Who else is calling appear below the groups when available. The contact's station is looked up on QRZ when the contact starts. These facts no longer occupy the top of the right column.

### Next in line

A grid with a header row. One row per station that is needed (tiers 1 to 4), callable (see `04-domain-logic.md`) and passes the filters. Sorted by the ranking rule. At most 12 rows; the rest are counted in a footer link "and N more".

| Column | Content |
| --- | --- |
| # | Rank, starting at 1 |
| Call | Callsign, semibold |
| Where | Country, or "State, USA" / "Province, Canada" when known, then distance. With a city from QRZ: "Springfield, IL, USA", "Ottawa, ON, Canada", "Wellington, New Zealand". City and state come from QRZ: stations in the list are looked up once (see `05-integrations.md` §4) and the answer is kept in the local cache, so it shows again at once after a refresh or restart. A row updates as soon as its lookup arrives, without waiting for the next re-rank |
| Why | Need tag: New country (caution, bold), New band (accent, bold), New grid with the station's 4-character grid, "New grid DO62" (accent, bold), New call (plain). A new band has the tooltip "Canada is in your log on 40 m and 15 m, not on 20 m yet" (owner request 2026-10-12). A new country on Club Log's most-wanted list also gets a rarity meter after the tag: five small cells, filled in caution colour up to its level, with the tooltip and screen reader text "Rare: #61 on Club Log's most-wanted list" (`04-domain-logic.md` section 15). The column is 160 px wide in Next in line and Repeats |
| Doing now | "Calling CQ, 6 slots" · "Calling CQ DX, 2 slots" · "Calling CQ, 4 slots, fading" · "Calling you" · "Calling CQ, missed last slot" / "Calling CQ, missed 3 slots" for a CQ caller kept listed after a miss |
| Last heard | Time since the end of the station's last decoded transmission: "45 seconds ago", "2 minutes ago". Counts up between slots, also while the list is held, secondary text |
| dB | Their latest SNR at my station |
| Signal | Sparkline of their SNR over their last 6 own slots, oldest on the left, gaps where they were not decoded. The scale fits the station's readings but spans at least 12 dB. Tooltip and screen reader text give the values |
| Chance | Good (bold), Fair, Long shot, followed in secondary text by what it is based on (owner request 2026-10-12, replacing a separate Hears you column): "hears you at -22" (direct report in the last 15 min), "heard in Japan at -17" (best report from the same entity, state or grid field), "no reports yet", or nothing without PSK Reporter. 230 px wide; a long reason is trimmed and shown whole in the tooltip |
| Last worked | The most recent log contact with the call on any band or mode, as in Repeats ("2026-10-03 · 40 m"), or "Never"; tooltip as in Repeats |
| Call | Button. Calls that station |

Behaviour:

- Row 1 is highlighted as the selected row when nothing else is selected. Up and Down move the selection. Enter calls the selected row. Double-click calls that row.
- Rows re-rank only at slot boundaries, after decodes arrive, and never while the pointer is over the list or within 300 ms of a click. A pending re-rank shows a "List updated" pill that applies on click or when the pointer leaves.
- Selecting a row loads its QRZ lookup and shows a details flyout to the right of the row: name (with nickname), location, county, grid, bearing, the station's local time, licence class, how it QSLs (LoTW, eQSL, paper, manager), CQ and ITU zones, the most-wanted rank ("Rare: #61 on Club Log's most-wanted list"), an "Open on QRZ.com" link and a "Photo" link when QRZ has one, and the operator's past contacts with that call (date, band, mode, confirmed). Lines with no data are left out; without a lookup it says "No QRZ details yet". The flyout refreshes when the lookup arrives. Under the facts, **Who else is calling** (when anyone has been heard calling the station, or it has answered callers in the last 10 minutes; `04-domain-logic.md` section 12): the headline, the current callers (call, dB at my station, audio offset), where my transmit offset sits among them, and how the station has been choosing. Lines with no data are left out; without a lookup it says "No QRZ details yet". The flyout refreshes when the lookup arrives.
- Local time uses QRZ's standard offset (`GMTOffset`) and daylight-saving flag (`DST`). Daylight saving is applied with the US/Canada rule or the EU rule; anywhere else that observes it, local time is left out rather than guessed.
- Empty state: "Nobody you need is calling CQ right now." with the Watching list still shown.

### Watching

Stations that are needed but not callable now. Columns: Call, Where, Why, signal sparkline (as in Next in line), reason. Reasons: "Went quiet 45 seconds ago", "In a contact with EA5HVK", "Finishing a contact with K7RGB". Rows are secondary text. Maximum 8 rows. When no row is a new country or new band, the list folds to one line under the title ("12 needed stations busy: 11 new calls, 1 new grid") with a "Show all" link; once opened it stays open ("Fold" link) until the operator folds it (product review 2026-10-12).

### Repeats

Directly under Next in line. Stations heard now that are already worked (tier 5: this call on this band and mode), so the operator can work them again. Title "Repeats", subtitle "Heard now and already in your log on 20 m FT8." Columns are the same as Next in line, at the same widths so the two tables line up (owner request 2026-10-11): #, Call, Where, Why ("Not confirmed" in accent bold, or "Confirmed" in secondary text, for this call on this band and mode: the usual reason to work a station again; owner request 2026-10-12), Doing now (or, when not callable, the Watching reason in secondary text; "Went quiet 45 seconds ago" counts up every second with Last heard), Last heard, dB, Signal sparkline, Chance with its reason, Last worked ("Today · 20 m", "Yesterday · 40 m", "2026-10-03 · 20 m": the most recent log contact with the call on any band or mode; tooltip gives the contact count, exact UTC time, band, mode and confirmation), and a Call button ("Answer" when the station is calling me; always enabled, also when not callable, owner request 2026-10-12; a station in a contact with someone else is still not called, the contact ends at once as "In another contact", see 04-domain-logic.md). Callable stations first, then those not confirmed, then the line's ranking order. At most 8 rows, then "and N more". The list is held still while the pointer is over it or within 300 ms of a click, as Next in line. Empty state: "Nobody you have worked on 20 m is on the air right now."

### Right column

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

**Calling you** (formerly "Waiting for you"; owner decision 2026-10-11): a bar pinned above the top slot, outside the scrolling area, shown only when someone has called me and I have not answered. Each caller shows callsign, where, need tag, "Called you at HH:MM:SS · SNR", and a button "Answer after this contact" (or "Answer" when idle). Up to 5 callers, oldest first. A station whose contact I abandoned (or that ended with no reply or by the watchdog) and that replies within 5 minutes is also shown, whatever it sent, with "Replied after you stopped: <message>" and a "Resume" button. In the station list, the button of a station calling me reads "Answer" and answers it the same way. An optional chime (Settings, Operating, off by default) plays on the computer's default speakers when a new caller appears; it stays silent if the default device is the radio's audio device or no radio audio device is set.

**Your signal on 20 m**: see Reach, below. Compact form, with no map (owner request 2026-10-12: Heard here's "Who heard you" rings already map these stations): headline counts, farthest, best report, the neighbour summary line when there is one ("7 dB weaker than stations near you into Europe."), the three newest reports (call, where, dB, age), link "Open reach view". Caption "PSK Reporter · live", or "· updated 6 min ago" on the query fallback, or "· unavailable".

**Worked tonight**: title with count, subline "N new country · N new bands". The three newest contacts: call · where, upload status ("Uploaded to QRZ", "Waiting to upload · retrying", "Upload failed · Retry"), need tag at the time of the contact, time. Link "Show all N in the log".

**How the line is ordered**: one sentence and a "Change" link that opens the ranking settings flyout: tier order (drag to reorder), and checkboxes Prefer stations that hear me (on), Only stations calling CQ (on), Hide stations worked on this band (on), Skip long shots (off).

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

## Raw decodes view

The classic list for operators who want it. Columns: UTC, local time, dB, DT, Hz, message, country, distance, why. Newest slot at the top (owner request, replacing newest at the bottom), blank line between slots, keeps scroll position when scrolled down away from the newest lines, with a "Jump to live" button. Messages to me in critical colour, CQ in accent. Double-click calls the sender. A filter box matches callsign, message text or country.

Owner request 2026-10-26 ("more feature rich"):

- **Distance** and **Why** come from the station list (the same text and colours as its Why column); blank for my own transmissions and for senders the list does not know.
- **Show** dropdown beside the filter, as in the Log view: All lines (default), CQs, To or from me (messages to me and my own transmissions), New country, band or grid. While the filter or Show hides lines, "5 of 212 lines" and a "Clear filters" link appear.
- Lines from the contact partner, and my transmissions to them, get the in-contact tint and left bar used on Operate.
- Right-click a line: Call <call>, Show only <call> (sets the filter), Copy line (`HHMMSS  dB  DT  Hz ~  message`, as WSJT-X prints it).

## Reach view

Mockup: `reach`. Band dropdown and time-window dropdown (15 minutes, 1 hour, 24 hours).

Layout (owner request): the map card with the plot and the by-band stats fills the top two thirds of the view; Who heard you fills the bottom third and scrolls on its own, with its column headings fixed.

- **Headline**: heard by N stations, countries, farthest distance, best report.
- **Plot**: azimuthal equidistant, centred on my grid, north up, rings every 2,000 miles (or 3,000 km), scale to the farthest report rounded up to the next ring. One dot per reporting station at its bearing and distance. Three strength classes by size and shade: strong (-10 or better), fair (-11 to -17), weak (-18 or below). Label dots with callsigns, with collision avoidance: drop labels that would overlap, show them on hover. This fixes the overlap visible in the mockup. When my grid is known, land and country borders (Natural Earth 1:110m, public domain, bundled) are drawn faintly behind the rings in the same projection, on both the full plot and the compact one.
- **Zoom**: the full plot zooms up to 16×. Mouse wheel zooms about the pointer; +, − and "Show the whole map" buttons sit beside the plot with the zoom factor under them. Drag to move around when zoomed; double-click shows the whole map. The frame, the N marker and line widths stay fixed; the map, rings, dots and labels scale, so more labels fit as you zoom in. The compact plot on Operate does not zoom.
- **Who heard you**: grid of station, where, miles, bearing, dB, age, "In your log" (Needed in accent, or Worked). Sortable; default by distance. Double-click a row opens that station's details.
- **By band, last 24 hours**: band, stations that heard you, farthest, median report. Current band highlighted.
- **Compared with stations near you** (under By band; `04-domain-logic.md` section 13): the summary line, "Compared with 9 stations within 300 miles, last 30 minutes.", and a row per continent: toward, my median, theirs, shared receivers, difference ("7 dB weaker" in caution colour, "Level", "3 dB stronger"). It covers the band and mode I am on: for another band it says "Shown for the band you are on, 20 m."; with too few shared receivers, "Not enough shared reports yet. It needs 3 receivers on one continent that heard both you and a station near you."; with the setting off, "Turned off in Settings, PSK Reporter."
- Empty state: "No reports yet. Reports appear a few minutes after you transmit."

## Log view

Grid of all contacts, newest first: date, time, call, band, mode, sent, received, grid, country, name, need tag at the time, QRZ status. Search box. Edit and delete (delete asks once; QRZ deletion is a separate explicit action). Buttons: Import ADIF, Export ADIF, Sync with QRZ now. Status line: "2,418 contacts · last synced 02:50 UTC".

## Awards view

Owner request 2026-10-13. Left column: a card each for DXCC (any band), WAS (50 US states; Alaska and Hawaii count though they are separate DXCC entities), WAZ (40 CQ zones, from the country file's zone for each call's prefix) and VUCC on 6 m (4-character grids). Each card shows worked and confirmed counts, a bar towards the award (confirmed in accent, worked faintly behind), the goal ("100 confirmed for the award · 340 in all") and, for WAS and WAZ, "Still needed" and "Worked, not confirmed yet" lists. Below, DXCC by band: every band with contacts and always 80, 40, 20, 15 and 10 m, with the 5-band DXCC line ("2 of 5 bands have 100 countries confirmed"). Right column: Countries still needed, for any band or one band, optionally with those worked but not confirmed; each row has the country, continent and status, and "when to look" from the operator's own decode journal ("Heard 14 times, mostly on 20 m, usually 13:00–15:00 UTC, last heard 3 Oct.", or "Not heard in your decodes yet."). Countries heard most often come first. Confirmed means the log's confirmed flag. Counting is `Core/Awards/AwardBuilder`; the sentence is `Core/History/WhenToLook`. A "time" is a distinct band and UTC hour. The journal is read in the background at start-up and again when the view or a station's details open and the copy is over 10 minutes old. The station details flyout shows the same sentence for the station's country ("Mongolia: heard 6 times, …") when there is history. Empty log: "Your log is empty. Sync with QRZ or import an ADIF file on the Log view, and your award progress appears here."

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

Sections: Station profiles · Radio · Audio · Transmit (power, watchdog minutes, retry limit, RR73 or RRR, CQ modifier, my grid in my answer to a CQ on or off, `73` or `RR73` as my last message when answering, optional free text up to 13 characters sent after it, a preview of every message as it will be sent, auto-log on or off) · Ranking (also how long CQ callers stay in Next in line after they were last heard: only while heard every slot, or 1 to 15 minutes, default 2) · QRZ · PSK Reporter (receive feed on or off, upload my spots on or off) · Interop (UDP address and port) · Files (WAV saving: none, decoded, all; retention days) · Mobile connection (on or off, applied at once; the address to open on the phone, shown large while it is on; no pairing code, owner decision 2026-10-25; port) · Appearance (skin, theme, units).

## Phone (remote page)

A single page served by the app to any phone on the home network (no pairing code since 2026-10-25) (owner decision 2026-10-12). Top: my call, frequency, band and mode, link state, radio dot, UTC, clock and decoder, and who has control ("You control", "Desk controls", "Another phone controls"). An ON AIR line with the message while transmitting. A yellow banner for the fault line (for example "Transmit stopped at 20:31:15: lost touch with the phone."). The contact card with the same wording and five-step strip as the desk, the messages around the current step (tap an upcoming one of mine to jump), and Resend, Log now and Abandon; when idle, Call CQ or Stop CQ. Calling you and Next in line (up to 8) with their buttons. HALT TX pinned to the bottom ("NOT TRANSMITTING" in grey when nothing is going out). The desk's bottom bar shows "Phone in control" or "Phone connected".

Richer page (owner request 2026-10-12, "more feature rich"): under the top bar, a slot countdown bar (red while transmitting), the Tx meter warning after a transmission outside limits, and one line when QRZ, PSK Reporter or audio is not working. Five tabs: Operate (contact card, Calling you, Next in line with the desk's columns: doing now, where, signal, hears you, chance, last heard, last worked), Stations (band summary figures, Repeats up to 8, Watching up to 8 with the reason), Decodes (band activity, newest first, up to 60 lines, coloured as on the desk), Signal (the desk's "Your signal" panel: headline, farthest, best, nearby stations, latest reports), Log (tonight's contacts). Tapping a call opens a station sheet with the desk's details flyout content and a Call or Answer button when the station can be called. Tapping the frequency opens a band sheet: mode, band (not while transmitting; confirms before ending a contact or CQ) and Tx offset (Automatic, or 200 to 2900 Hz), with the desk's wording and saved to settings. Opening details does not take control; band and offset changes do. The tab in use is remembered on the phone.

Tablet layout (owner request 2026-10-12): at 760 px wide and up the same page shows the desk's Operate view at once, with two tabs (Operate, Decodes). Next in line and Repeats are tables with the desk's columns (Call with need tag, Where, Doing now, Signal, Chance with its reason, Last heard, Last worked, button); Watching shows Call, Where and why not now. Portrait (760 to 1079 px) is one column and drops Last worked; landscape (1080 px and up) adds a 340 px right column with the band summary, your signal and tonight's contacts. The contact card shows all five messages under the strip. Touch targets are at least 44 px; station details open as a right-hand panel; HALT TX is a 420 px button pinned bottom right.

Home-screen app (owner request 2026-10-20, "make it a PWA"): the page links a web app manifest (`display: standalone`, Costas icon) and the Apple home-screen tags, so Add to Home Screen gives an icon that opens full screen on iOS and iPadOS. There is no service worker and no install prompt, because both need a secure (HTTPS) origin and the page is plain HTTP on the LAN. Chrome on Android adds a shortcut that opens in a tab. The top bar and the tablet side panel pad for the status bar and notch.


Audio dropouts, decode time per slot, median DT of decodes, clock offset, CAT round-trip time, ALC and SWR of the last transmission, `jt9` and `rigctld` process state and restarts, QRZ and PSK Reporter connection state and last error, upload queue length. A button "Save diagnostics bundle" writes a zip of logs and settings (with secrets removed).

## Keyboard

| Key | Action |
| --- | --- |
| Enter | Call the selected station; in a contact, nothing |
| Up, Down | Move selection in the line |
| Esc | Halt Tx. In a contact: abandon it. Once both reports are exchanged, the first press stops transmitting but keeps the contact (a late RR73 is still logged); the second abandons (owner decision 2026-10-20) |
| Ctrl+Q | Call CQ / stop CQ |
| Ctrl+R | Resend |
| Ctrl+L | Log now |
| Ctrl+1 to Ctrl+5 | Switch view |
| Ctrl+M | Mini mode |
| F1 | Keyboard shortcuts |

## States every data-bound element must handle

Loading (skeleton or "—", never a spinner over the whole view), empty, stale (secondary text with age), and error (inline text with a retry action). No modal dialogs during operating except the confirm for Abandon after reports are exchanged.

## Accessibility

Every control has an accessible name. The line and grids are keyboard navigable. Contrast at least 4.5:1 for text. Tx state is announced to screen readers when it starts and stops.
