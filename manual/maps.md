# Maps and who hears you

Costas has two maps. **Heard here** shows who you hear. **Reach** shows who hears you, from PSK Reporter. Both are
centred on your grid square, so set it in [Settings › Station](settings.md#station) first.

## Heard here

The first card in the right column of Operate. Each dot is a station you decoded in the last 15 minutes. Hover over
a dot for its call, place, need, report and when it was heard. Your choices are remembered.

<div class="grid" markdown>
![Heard here, Around me](images/heard-here-around.png){ width="300" }
![Heard here, World with day and night](images/heard-here-world.png){ width="300" }
</div>

| Control | Options |
| --- | --- |
| View | **Around me**: centred on you, north up, with distance rings. **World**: a flat world map |
| Show | **Everyone heard**, **Needed only**, **Calling CQ** |
| Colour by | **Need** (new country amber, new band or grid blue, new call black, worked grey), **Signal** (strong, fair, weak), **Last heard** (last minute, 1 to 5 minutes, 5 to 15 minutes) |
| **Who heard you** | Adds a hollow ring for each PSK Reporter receiver that heard you in the last 15 minutes. A station you hear that also hears you shows as a dot inside a ring |
| **Day and night** | World view only: shades the night side of the earth, so you can see the greyline |

The legend under the map says in words what each colour means. Labels that would overlap are dropped; hover to see
them. Without a grid, the map falls back to the World view and asks you to set your grid.

## Reach view

Press **Reach** in the top bar (++ctrl+3++), or **Open reach view** under *Your signal* in the right column.

![Reach view, before any reports](images/reach-empty.png)

- **Band** and **Window** (15 minutes, 1 hour, 24 hours) choose which reports to show.
- **Headline**: heard by how many stations, in how many countries, the farthest and the best report.
- **Plot**: you are at the centre, north is up, with rings every 2,000 miles (or 3,000 km). Each dot is a station
  that reported you, at its bearing and distance. Bigger, darker dots are stronger reports: strong (-10 dB or
  better), fair (-11 to -17), weak (-18 or below).
- **Who heard you**: the reporting stations with where, distance, bearing, dB, age and whether they are in your log.
  Sortable. Double-click a row for that station's details.
- **By band, last 24 hours**: how many stations heard you on each band, the farthest and the median report.
- **Compared with stations near you**: is it the band, or your station? PSK Reporter also hears the other stations
  within 300 miles of you. For every receiver that heard both you and one of them in the last 30 minutes, Costas
  compares the two reports, then sums it up per continent: *7 dB weaker than stations near you into Europe.* A
  continent shows once 3 receivers have heard both. The same line appears under *Your signal* on the Operate screen.
  Bear in mind a neighbour running more power than you makes you look weaker.
- **Did a change help?**: Costas keeps a daily summary of that comparison, so you can see whether a new antenna
  or amplifier made a difference. For the band and mode chosen at the top, each continent gets a line such as
  *Into Europe: 4 dB behind neighbours this past week, 7 dB behind the week before.* and a small chart of the last
  30 days: one dot per day, the middle line is level with stations near you, below it you were weaker.
  Press **Mark a change today** on the day you change something (type what changed first if you like). The lines
  then compare the days since with the two weeks before: *Into Europe: 2 dB behind neighbours since your change on
  9 Oct, 7 dB behind in the two weeks before.* The day you mark counts as after the change. Pressed again the same
  day, the button removes the mark.

  What is stored: every five minutes, while the comparison has something to show, the difference for each continent
  is added to that day's summary (UTC days), per call, band and mode. Only these counts and your marks are kept, in
  `neighbour-history.json` in the Costas data folder, for about a year. No reports or calls of other stations are
  stored. Nothing is recorded while the comparison is turned off in Settings, or in simulation. Deleting the file
  starts the history again.

### Band activity near you

Open **Reach** or the **Band** dropdown to see, before you switch, which bands are open from your area right now. For each band it shows how many PSK Reporter spots stations within 300 miles of you got in the last 15 minutes, which continents heard them (darker means more), and countries hearing them that you still need on that band, for example *You need Brazil +2*. Hover over it for the full list.

It needs **Receive live reports of stations hearing my signal** and **Show band activity near me in Reach and the band menu** in [Settings](settings.md). The rows fill in once PSK Reporter has reported your own signal, which tells Costas your country; Costas remembers it, so after that they fill in at start-up. Until then each row says *Needs a spot of you*. If PSK Reporter sends more reports than Costas can keep up with, it follows only the band you are on and the other rows say *Too busy to follow* until you restart Costas.

Reports appear a few minutes after you transmit. Until then the view says *No reports yet*.

The populated Reach view looks like this design mock-up (sample data):

![Reach view mock-up with sample reports](images/reach-mockup.png)

<!-- TODO screenshot: replace the mock-up with a real Reach view once the app has been on the air with PSK Reporter. -->

## Where the data comes from

Reports of your signal come from [PSK Reporter](https://pskreporter.info/), live through its MQTT feed, with a
periodic query as a fallback. The caption under *Your signal* says which: *PSK Reporter · live*,
*· updated 6 min ago* (fallback), or *· unavailable*. Turn the feed on or off in
[Settings › PSK Reporter](settings.md#psk-reporter). Without it, the maps and the station list still work, but
Chance comes from the signal alone.
