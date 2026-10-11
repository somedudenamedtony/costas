# The Operate screen

Operate is where you spend your time on the air. Everything you need during a session is on this one screen; you
should not need Settings while operating.

![The Operate screen](images/operate-idle.png)

## Top bar

| Item | What it does |
| --- | --- |
| **Band** | Changes band. Costas sets the radio's dial frequency, clears the station list and loads your log totals for the new band. Locked while transmitting |
| **Mode** | FT8 or FT4. Changes the slot length and dial frequency. Locked while transmitting |
| Frequency | The dial frequency read from the radio, in MHz |
| **Operate**, **Raw decodes**, **Reach**, **Log** | The four views. ++ctrl+1++ to ++ctrl+4++ |
| Clock | UTC. Turns amber with a tooltip when your clock is off by more than 0.5 s, red above 2 s |
| **☰** | Settings, Station setup, Diagnostics, Keyboard shortcuts, About |

![The menu](images/menu.png)

Two banners can appear under the top bar:

- **ON AIR** (red) while Costas is transmitting, with the message being sent and a **Halt (Esc)** button.
- **Radio not connected** (amber) when the radio is not set up or not answering, with a **Station setup** button.

## The top slot: band summary

When you are not in a contact, the top card summarises the band.

**20 m right now** (named after the current band): stations heard in the last 5 minutes, how many countries, how
many are calling CQ, and **You need** (stations that are new to you in some way). The line under it gives decodes
in the last slot and the average per slot over the past hour.

**Heard from**: a bar per continent (N. Am., Europe, Asia, Oceania, S. Am., Africa).

**Your log on 20 m**: countries worked and confirmed on this band, US states worked (and the first few you still
need), grid squares worked, and **Tonight**: contacts, new countries and new bands this session. A session starts
when you open Costas, or after 2 hours with no contact.

When you call or answer a station, this card becomes the contact panel. See [Making contacts](contacts.md).

## Next in line

The stations you need that you can call right now, best first. At most 12 rows; the rest are counted underneath.

| Column | Meaning |
| --- | --- |
| **#** | Rank |
| **Call** | Callsign |
| **Where** | Country (or state for the USA, province for Canada) and distance |
| **Why** | Why you need them: **New country**, **New band**, **New grid** or **New call** |
| **Doing now** | *Calling CQ, 3 slots*, *Calling CQ DX*, *Calling you*, and *fading* when their signal is dropping |
| **Last heard** | How long ago they were last decoded |
| **Hears you** | From PSK Reporter: *Yes, -22* (they reported you), *Japan does, at -17* (someone in their country or area did), *No reports yet*, or *—* without PSK Reporter |
| **dB** | Their signal at your station |
| **Signal** | A sparkline of their last 6 slots, gaps where they were not decoded. Hover for the values |
| **Chance** | **Good**, Fair or Long shot, from whether they hear you and how strong they are |
| button | **Call**, or **Answer** when they are calling you |

How the order works: stations are grouped by need (new country first), then by chance, then by how well they hear
you, then by their signal. The **ⓘ** beside the title shows the current rules in one sentence; click it to change
them (the same options as [Settings › Who to call](settings.md#who-to-call)).

The list never reshuffles under your mouse. If it needs to re-rank while you are pointing at it, a **List updated**
pill appears; it applies when you click it or move the pointer away.

Selecting a row (click, or ++up++ / ++down++) shows a details card with the station's name (from QRZ lookups),
location, grid, bearing and your past contacts with that call:

![Station details](images/station-details.png)

To call a station: click its **Call** button, double-click the row, or select it and press ++enter++.

!!! note "Chance without PSK Reporter"
    If PSK Reporter is off or unreachable, chance is worked out from their signal alone: Good at -10 dB or better,
    Fair at -18 or better, otherwise Long shot.

## Repeats

Directly under Next in line: stations you can hear now that are already in your log on this band and mode. Next in
line leaves them out because you do not need them, but you may still want to work them again: a friend, a regular,
or someone you owe a better report. At most 8 rows; the rest are counted underneath.

| Column | Meaning |
| --- | --- |
| **Call** | Callsign |
| **Where** | Country (or state, or province) and distance |
| **Doing now** | As in Next in line. When they cannot be called right now it says why instead, in grey: *In a contact with EA5HVK*, *Went quiet 2 minutes ago* |
| **Last heard** | How long ago they were last decoded |
| **dB** | Their signal at your station |
| **Signal** | A sparkline of their last 6 slots |
| **Last worked** | When and on which band you last worked them, on any band or mode: *Today · 20 m*, *Yesterday · 40 m*, *2026-10-03 · 20 m*. Hover for the number of contacts, the exact time, the mode and whether it is confirmed |
| button | **Call**, or **Answer** when they are calling you. Greyed out while they cannot be called |

Stations you can call now come first, then the same order as Next in line. Like Next in line, the list holds still
while your pointer is over it and catches up when you move away. When nobody you have worked on this band is on the
air, it says so.

## Watching

Stations you need that you cannot call yet: they went quiet, or are in a contact with someone else
(*In a contact with EA5HVK*, *Finishing a contact with K7RGB*). They move into Next in line when that changes.

Stations you have already worked on this band are not in Watching; they are in [Repeats](#repeats).

## Right column

From top to bottom:

- **Heard here**: a map of the stations you have decoded in the last 15 minutes. See
  [Maps and who hears you](maps.md#heard-here).
- **Band activity, last hour**: a bar per minute of decodes per slot (grey) and needed stations per slot (blue),
  with a sentence saying whether the band is busier or quieter than half an hour ago.
- **Your signal on 20 m**: a small Reach map, how many stations hear you, the farthest and the best report.
  **Open reach view** goes to the full view.
- **Worked tonight**: your latest contacts this session with their QRZ upload state. **Show all in the log** opens
  the Log view.

![Lower part of the right column](images/right-column-lower.png){ width="320" }

## Bottom bar

| Item | What it does |
| --- | --- |
| Status text | One line: *Not in a contact. Enter calls NT2A, the top of the line.*, *Transmitting to F5RXL.*, *Waiting for F5RXL.*, or a fault in red |
| **Tx offset 1650 Hz · clear** | Your transmit audio offset. Click to choose **Automatic (picks a clear spot)**, the default, or type a fixed value (200 to 2900 Hz) and press **Use** |
| **Call CQ** | Starts calling CQ in the next slot. Becomes **Stop CQ** while running. ++ctrl+q++ |
| **Halt Tx** | Always available. Drops PTT at once and stops sequencing. ++esc++ does the same |
| Slot progress | *Receiving* or *Transmitting*, seconds to the next slot, and a bar |
| Meter warning | Appears only when the last transmission's SWR or ALC was high, for example *SWR 3.4 · ALC 70%* |
| Indicators | **Radio**, **Audio**, **QRZ**, **PSK Reporter**: a dot and a word each. Click to open Diagnostics |

![Transmit offset flyout](images/tx-offset.png)

Costas has no waterfall. With the offset on Automatic it picks a clear spot in the passband for you each time.
