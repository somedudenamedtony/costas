# Awards and when to look

Press **Awards** in the top bar or ++ctrl+5++.

The Awards view shows how far your log is towards the common awards, and which countries you still need, with a note
on when you actually hear each one. Everything comes from your own log and your own decodes; nothing is looked up
online.

## Award progress

The **Count** menu at the top picks which contacts count: **All digital** (the default: FT8, FT4, PSK, RTTY and other
data modes, as ARRL's DXCC Digital award counts them), **All modes** (phone and CW too), or a single mode.

The left side has a card for each award:

| Card | What it counts |
| --- | --- |
| **DXCC** | Countries (DXCC entities) on any band. The award needs 100 confirmed |
| **WAS** | The 50 US states. Alaska and Hawaii count, though they are separate DXCC countries. A contact counts for a state only when the state is in your log (QRZ lookups and most ADIF files fill it in) |
| **WAZ** | The 40 CQ zones. The zone comes from the station's callsign prefix, so a station operating away from home can be counted in the wrong zone |
| **VUCC** | Grid squares (the first four characters) on 6 m. The award needs 100 confirmed |

Each card shows how many you have **worked** (at least one contact) and **confirmed**. The bar shows confirmed towards
the award, with worked shown faintly behind it. The WAS and WAZ cards also list what is still needed and what is
worked but not confirmed yet; hold the pointer over the list of zones to see which countries are in each.

**Confirmed** means the contact is marked confirmed in your log, for example by a QRZ logbook confirmation or a
confirmed contact in an imported ADIF file.

### DXCC by band

Under the cards, a table gives worked and confirmed countries on each band you have used, and always on 80, 40, 20,
15 and 10 m, the five bands of **5-band DXCC**. The line above the table says how many of those five bands have 100
countries confirmed.

## Countries still needed

The right side lists every country you have not worked yet. Choose a band to see the countries you still need on that
band, and tick **Include worked but not confirmed** to add the ones you are waiting on a confirmation for.

## When to look

Next to each country is a note from your own receive history: every decode Costas has written to the
[decode journal](log.md#decode-journal-and-saved-audio). For example:

- "Heard 6 times, always on 20 m between 13:00 and 15:00 UTC, last heard 3 Oct."
- "Heard 14 times, mostly on 20 m, usually 13:00–15:00 UTC, last heard yesterday."
- "Not heard in your decodes yet."

A **time** is an hour in which you decoded at least one station from that country on one band, so a station calling
CQ for half an hour counts once. Times are UTC. The countries you hear most often come first in the list, because those
are the ones you have a real chance of working from your station.

The same note appears in a station's details on the Operate view, for that station's country.

Costas reads the journal in the background when it starts, and again when you open the Awards view or a station's
details if it was last read more than ten minutes ago. A line under the list says how many decodes the notes come
from. With a large journal the first read can take a few seconds; the list fills in when it is done.
