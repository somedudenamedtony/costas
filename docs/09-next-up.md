# 09 · Next up

Proposal list as of 2026-10-16, for the owner to choose from. Nothing here is approved or scheduled until the owner picks it. Living copy: https://claude.ai/code/artifact/47282a3b-f3bf-40f3-8ad8-756f15f6a2b7

## Top picks

Do these five next: two fix things that cost contacts or hide controls today, one turns untested work into known-good, and two add capability operators will notice. Sizes: S = a small PR, M = a normal feature PR, L = several PRs.

1. **Fix the Operate screen at the default window size (S).** At 1440x900 the Next in line and Repeats columns take 1,148 px, so "Doing now" has no width and the Call button is cut off (bug-hunt U4). It is the first thing a new user sees.
2. **Contact-flow fixes that lose QSOs (M).** An RR73 on the last retry is thrown away and the contact is not logged (T4); logging runs on the slot thread and can hold PTT past the audio (T3); clicking a station now calling CQ can send an old R-report (T8); compound calls like PJ4/K1ABC can't be clicked (T7, T11).
3. **One on-air test session with a checklist (S).** Auto ALC thresholds, follow-the-radio's-band, FLRig, decode timing and the PSK/RTTY branch are all untested on the FT-710. A one-page checklist plus a Diagnostics "save test report" button turns one evening on the air into a list of real fixes.
4. **Ask before abandoning a contact (S).** Band and mode menus, dial changes and Hunt "Go to 40 m" end a contact or CQ without asking, while the phone remote asks. Pair it with the owner's Esc decision so Esc in a text box stops ending QSOs.
5. **SuperFox decoding, which unlocks SuperHound (M).** Most big DXpeditions now run SuperFox; today Costas can't decode them, so they can't be worked the Hound way.

## UX polish

These keep the current layout and fix what makes it awkward. Ordered by how often an operator hits them.

| # | Item | Why it helps | Size |
| --- | --- | --- | --- |
| U1 | Operate screen fits at 1440x900 and narrower | Call button and "Doing now" are cut off at the default size today | S |
| U2 | Ask before a band, mode, dial or Hunt "Go to" change ends a contact | Matches the phone remote; no surprise lost QSOs | S |
| U3 | Esc in a text box no longer ends the contact (owner's call) | Clearing Log search currently abandons a QSO | S |
| U4 | Countdowns that tick: contact card "N s left", Repeats, "Went quiet N s ago" | They freeze between updates and read as stale | S |
| U5 | "Hide stations I've already worked on this band" actually hides them | The setting exists but does nothing | S |
| U6 | Status line gets a tooltip and room at narrow widths | Fault messages are cut to ~110 px and unreadable | S |
| U7 | Remember window position and maximized state; keep it on a visible screen | Costas can reopen off-screen or at -32000 after closing minimized | S |
| U8 | One copy at a time, and a visible error if start-up fails | A second copy fights over the COM port; a locked database just means Costas doesn't open | S |
| U9 | Phone remote keeps the phone screen awake during a contact | iPhone auto-lock drops the connection and halts Tx mid-QSO | S |
| U10 | Units and band names right everywhere ("300 miles" in km mode, raw band keys) | Small, but visible on every tooltip | S |
| U11 | Colours update when Windows switches light/dark | Some brushes stay on the old theme until restart | S |
| U12 | Faster Log search and no empty-callsign edits | Search re-queries up to 5,000 rows per keystroke | S |

## New capabilities

Ordered by value to an FT8 operator against effort. Anything that changes what goes on air (C3, C6) needs the owner's sign-off on the exact behaviour before building.

| # | Item | Why it helps | Size |
| --- | --- | --- | --- |
| C1 | SuperFox decoding, then SuperHound | Most large DXpeditions use SuperFox; Costas can't hear them yet | M |
| C2 | Club Log real-time upload | Club Log already feeds the Rarity meter; uploading closes the loop for DXCC tracking and OQRS | S |
| C3 | Compound and portable calls (type 4 messages) | VE3/W7LIT as your own call, or answering PJ4/K1ABC, fails today | M |
| C4 | DX cluster and RBN spots, display only, beside POTA spots | Shows rare stations on other bands before they show up on the waterfall | M |
| C5 | Band advice line: "15 m is open to Europe for you" | Already in the spec; builds on Other bands right now and the neighbour data | S |
| C6 | Contest exchanges (ARRL RTTY Roundup, Field Day, WW Digi) | Contest CQs are only parked in Watching today; weekends are when FT8 is busiest | L |
| C7 | More rigs: OmniRig and TCI (SunSDR, ANAN) | In the spec's v1.x list; TCI also carries audio, so no sound-card setup | M each |
| C8 | Automatic log backup and per-profile ADIF export with QSL state | Protects the log; today's export mixes profiles and drops QSL status | S |
| C9 | Audio device comes back after USB re-plug or sleep | Today a reconnected sound card needs a Costas restart | S |
| C10 | Linux transmit, once a Linux user confirms clean receive | Turns the preview into a full client; needs one tester | S |

## Reliability and finishing

These aren't new features, but each one is a known gap from the bug hunts or work waiting on the radio.

| # | Item | Why it helps | Size |
| --- | --- | --- | --- |
| R1 | Contact-engine fixes T3, T4, T5, T7, T8, T9, T11 from the main bug hunt | Lost logs on the last retry, PTT held during logging, stale reports, unrecognised Fox calls ended early | M |
| R2 | On-air test checklist and Diagnostics test report | Auto ALC, follow band, FLRig and decode timing have never run on the FT-710 | S |
| R3 | PSK/RTTY branch: owner's on-air test, then merge into main | Built and bug-hunted; needs the small waterfall merge from PR #82. Three bugs still open (CW in PSK channel, close RTTY stations merge, no recordings) | M |
| R4 | Log data fixes: Awards honour "confirmed by", UTF-8 ADIF names, QRZ errors not cached as misses, QRZ upload status returns to OK | Wrong award counts and dropped imports are hard to spot | S |
| R5 | Clock guard retries SNTP after a failure and uses Setup's clock check | A fixed clock can stay blocked for up to an hour | S |
| R6 | Settings that fail to load don't stop start-up | A hand-edited or locked settings file means Costas won't open | S |

The item codes (T3, U4, D10 and so on) refer to the main and PSK/RTTY bug-hunt reports kept in the project's shared files (`reviews/main-bug-hunt.md`, `reviews/psk-rtty-bug-hunt.md`).

## Left out on purpose

- **Already rejected:** the "Why it didn't finish" replay, the Ctrl+K command bar, and standalone audio-offset or waterfall panels.
- **Waiting on the owner's call, not re-proposed:** in-app clock correction, the Fake It split, and ranking new POTA parks higher (from the October 12 review).
- **Dropped by the owner on October 13:** PSK Reporter graph, Hamlib error, globe spin, About box.
- **Settled:** no first-run metrics notice, no extra wait cycles, waterfall/settings/log editing stay desk-only on the phone, Halt Tx stays in the bottom bar.
- **Not planned yet:** other modes (Q65, WSPR, MSK144), macOS builds and translations stay in the spec's "later" list.
