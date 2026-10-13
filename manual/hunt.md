# Hunt list and alerts

The Hunt list is what you are looking for: a country (*Alaska on 40 m*), a call (*VK9XA, any band*), a US state, a
grid, or *any new country*. When one is heard, Costas tells you, even while you are in mini mode, on another band, or
away from the desk with the phone page open.

Costas only tells you. It never changes band, calls anyone or transmits because of an alert.

## Adding entries

- In a station's details (click a row in Next in line), choose **Hunt this call** or **Hunt** *country*.
- Or open [Settings › Hunt list](settings.md#hunt-list), pick what kind of entry it is, type it, choose a band or
  *Any band*, and click **Add**. A country can be typed as a prefix (`KL7`, `VK9X`) or a name (`Alaska`); a state as
  `TX` or `Texas`; a grid as `FN31`.

An entry stops alerting once it is in your log on its band, or on any band for an any-band entry. With
**Count a country or state as worked only when confirmed** on, a country keeps alerting until it is confirmed, and so
do a state or grid on an any-band entry.

## Where Costas looks

- **Your own decodes**, on the band you are on. Every kind of entry is matched here. States come from QRZ lookups, so
  a state entry needs QRZ lookups set up.
- **Every other band, through PSK Reporter.** Countries and calls on your list are followed in PSK Reporter's live
  feed. A report counts only when a receiver within 1,000 km (about 620 miles) of you heard the station in the last
  15 minutes, so the station is likely workable from your area. Change the distance in Settings. This needs the live
  feed turned on in [Settings › PSK Reporter](settings.md#psk-reporter).

## What an alert looks like

- A bar under the top bar: *Christmas Island (VK9X): VK9XA is on 17 m, heard 300 mi from you at -14 dB.* **Go to
  17 m** changes band (and mode, if the station is on FT4). **Dismiss** hides the bar.
- The band menu names hunted stations heard on each band in the last 15 minutes, for example
  *Hunting: VK9XA (Christmas Island)*.
- Mini mode shows the alert as an extra line, with ✕ to dismiss it.
- The [phone page](phone.md) shows it as a banner under the top bar.
- The chime plays and Costas flashes in the taskbar, unless you turned that off. The chime plays on the computer's
  default speakers, never on the radio's audio device.

Each entry alerts once per band and then stays quiet on that band for 30 minutes, however often the station is heard.
