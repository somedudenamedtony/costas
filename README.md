# FT8 Client — build package

Everything needed to start building a modern FT8/FT4 client for Windows (cross-platform later). Hand this folder to Claude Code as the root of a new repository.

Owner: Tony Shepherd, W7LIT, grid DN40. Working name: **FT8 Client** (namespace `Ft8Client`). The product name is not chosen yet; keep it in one constant so it can be renamed.

## What this is

A station-centric FT8 operating app. Instead of a scrolling list of decoded messages, it shows one row per station, ranked by what the operator needs (from their QRZ logbook), whether the station is free, and whether the station can hear them (from PSK Reporter). It reuses the WSJT-X decoder and Hamlib, and replaces everything the operator touches.

## How to use this package

1. Create an empty git repository and copy this folder's contents into it.
2. Open Claude Code in that repository. `CLAUDE.md` is loaded automatically and tells it how to work.
3. First prompt: *"Read README.md and everything in docs/. Then start Milestone 0 in docs/07-milestones.md. Stop and report when its acceptance tests pass."*
4. Work one milestone at a time. Each has acceptance tests; do not start the next until they pass.

## Contents

| File | What it holds |
| --- | --- |
| `CLAUDE.md` | Working rules for Claude Code: stack, commands, conventions, safety rules for transmitting |
| `docs/01-product-spec.md` | Goals, users, scope, feature list by release |
| `docs/02-ui-spec.md` | Every screen, component, state, keyboard shortcut and design token |
| `docs/03-architecture.md` | Solution layout, components, threading, process model, simulation mode |
| `docs/04-domain-logic.md` | Message parsing, station tracking, need tiers, ranking, contact state machine, transmit encoding |
| `docs/05-integrations.md` | jt9 decoder, Hamlib, QRZ logbook and lookup, PSK Reporter (feed, query, upload), WSJT-X UDP, time check |
| `docs/06-data-model.md` | SQLite schema, settings, files on disk, ADIF mapping |
| `docs/07-milestones.md` | Build order, M0 to M8, with acceptance tests |
| `docs/08-open-questions.md` | Decisions assumed, facts not yet verified, known risks |
| `mockups/` | The approved screens as standalone HTML plus PNG screenshots |

## Mockups

| Screen | Files | Notes |
| --- | --- | --- |
| Operate, idle | `mockups/main-idle.html`, `.png` | Band summary on top, ranked line, watching list, right column |
| Operate, in a contact | `mockups/main-in-contact.html`, `.png` | Same layout; the top slot becomes the contact |
| Reach | `mockups/reach.html`, `.png` | Full view of who is hearing the operator |
| First-run setup, radio step | `mockups/setup.html`, `.png` | One of six setup steps; the others follow its pattern |

The HTML files are static renders with made-up sample data. Open them in a browser, or read their inline styles for exact sizes and colours. The PNGs were rendered on Linux, so the font is a fallback; the real app uses Segoe UI Variable. Two known flaws in the mockups to fix in the build, not copy: callsign labels overlap near the centre of the reach plot, and the right column makes the main window taller than 900 px. The setup mockup covers step 2 only; the other steps follow its pattern.

## Decisions already made

- C# on .NET 10 with Avalonia 12 for the UI. Windows first; nothing may block macOS and Linux later.
- Open source under GPLv3, because the app ships and runs the WSJT-X `jt9` decoder.
- The decoder is not rewritten. `jt9` runs as a child process.
- Rig control goes through Hamlib's `rigctld` as a child process.
- No waterfall on the main screen. The app picks a clear transmit offset itself.
- The operator starts every contact. The app never transmits on its own initiative.

`docs/08-open-questions.md` lists what is assumed and what still needs confirming.
