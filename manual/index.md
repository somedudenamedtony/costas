---
title: Costas, a digital-modes program for Windows
description: Free FT8, FT4, PSK31, PSK63 and RTTY program for Windows that shows you stations, not messages.
hide:
  - navigation
  - toc
---

<div class="cx-home" markdown>

<section class="cx-hero" markdown>
<div class="cx-hero__text" markdown>

<h1>Stations, not messages.</h1>

<p class="cx-lead">Costas is a free FT8, FT4, PSK31, PSK63 and RTTY program for Windows. It uses the same decoder as
WSJT-X, so it decodes as well and sounds the same on the air. What changes is the screen: one row per station, ranked
by what you need for your log, whether the station is free, and whether it can hear you.</p>

<div class="cx-actions">
  <a class="md-button md-button--primary cx-download" href="https://github.com/somedudenamedtony/costas/releases/latest/download/CostasApp-win-Setup.exe">Download for Windows</a>
  <a class="md-button" href="introduction/">Read the manual</a>
</div>

<p class="cx-meta"><span data-cx-version>Latest release</span> · Windows 10 and 11, 64-bit ·
<a data-cx-linux href="https://github.com/somedudenamedtony/costas/releases/latest">Linux (preview, receive only)</a> ·
<a href="https://github.com/somedudenamedtony/costas">Source on GitHub</a></p>

</div>
<figure class="cx-hero__shot">
  <img src="images/contact-in-progress.png" alt="The Operate screen during a contact: a progress strip of each message, station details, and the Next in line list" width="1440" height="900">
</figure>
</section>

<section class="cx-section" markdown>

## What it does

<div class="cx-features" markdown>

<div class="cx-feature" markdown>
### Who to call next
*Next in line* puts the stations you need first: a new country, then a new band, grid or call, worked out from your
own log and QRZ. Stations you have worked sit under it in *Repeats*.
</div>

<div class="cx-feature" markdown>
### Who can hear you
PSK Reporter and the Reverse Beacon Network show whether each station, or someone near it, hears your signal, with a
Good, Fair or Long shot chance of completing.
</div>

<div class="cx-feature" markdown>
### One-click contacts
Click **Call** or press Enter. Costas runs the standard exchange, shows each step on a progress strip and logs the
contact when it completes. A recognised DXpedition Fox is worked the Hound way automatically.
</div>

<div class="cx-feature" markdown>
### FT8, FT4, PSK and RTTY together
PSK31, PSK63 and RTTY sit in the same mode picker on the same screen, with typed macros, callsigns picked out of the
text and click-to-read on the waterfall.
</div>

<div class="cx-feature" markdown>
### Logging and confirmations
A local log with ADIF import and export, uploaded to QRZ, LoTW (through TQSL) and eQSL as you go, with confirmations
synced back. DXCC, WAS, WAZ and VUCC progress comes from your own log.
</div>

<div class="cx-feature" markdown>
### Hunt list alerts
List the countries, calls, states or grids you are after and get an alert when one is heard, on your band or, through
PSK Reporter, any other. Alerts never transmit.
</div>

<div class="cx-feature" markdown>
### Operate from your phone
A remote page served by Costas on your home network lets you call, answer and halt from a phone or tablet. Mini mode
shrinks the desk app to a strip that stays on top.
</div>

<div class="cx-feature" markdown>
### Looks after your radio
Transmit level adjusts itself per band from your radio's ALC reading, with live power, ALC and SWR meters. Costas
follows your radio's band, and works with Hamlib, FLRig and the WSJT-X UDP protocol for GridTracker and JTAlert.
</div>

</div>
</section>

<section class="cx-section" markdown>

## A look around

<div class="cx-gallery">
  <figure>
    <a href="images/operate-idle.png"><img src="images/operate-idle.png" alt="The Operate screen while receiving" loading="lazy" width="1440" height="900"></a>
    <figcaption>The Operate screen while receiving</figcaption>
  </figure>
  <figure>
    <a href="images/psk31-operate.png"><img src="images/psk31-operate.png" alt="PSK31 on the Operate screen" loading="lazy" width="1440" height="900"></a>
    <figcaption>PSK31 on the same screen</figcaption>
  </figure>
  <figure>
    <a href="images/awards.png"><img src="images/awards.png" alt="Awards progress" loading="lazy" width="1440" height="900"></a>
    <figcaption>Awards progress from your own log</figcaption>
  </figure>
  <figure>
    <a href="images/log.png"><img src="images/log.png" alt="The log" loading="lazy" width="1440" height="900"></a>
    <figcaption>Your log, with QRZ, LoTW and eQSL status</figcaption>
  </figure>
  <figure class="cx-gallery__phone">
    <a href="images/phone-contact.png"><img src="images/phone-contact.png" alt="The phone remote during a contact" loading="lazy" width="780" height="1688"></a>
    <figcaption>The phone remote</figcaption>
  </figure>
</div>

<p class="cx-note">Screenshots are from the real app replaying recorded band audio. Callsigns are from those
recordings.</p>

</section>

<section class="cx-section cx-promises" markdown>

## What it never does

- **It never transmits on its own.** Every transmission starts with something you clicked or pressed.
- **It never changes a message silently.** If a message cannot be sent exactly as shown, it refuses and tells you why.
- **It never shows made-up data.** With no radio set up it receives nothing and says so.

</section>

<section class="cx-section cx-start" markdown>

## Get started

1. **Download** [the Windows installer](https://github.com/somedudenamedtony/costas/releases/latest/download/CostasApp-win-Setup.exe).
   It installs for your user only and updates itself. See [Install](install.md) if Windows warns about it.
2. **Set up** your station, radio, audio and clock in the six-step [first-run setup](setup.md).
3. **Operate**: read [The Operate screen](operate.md) and [Making contacts](contacts.md).

Costas is free software under the GNU GPL, version 3. It is young: use low power or a dummy load for your first
transmissions. Questions and bug reports go to
[GitHub issues](https://github.com/somedudenamedtony/costas/issues).
[Code signing policy](code-signing.md) · [Privacy](settings.md#privacy)

</section>

</div>
