# 06 · Data model

## Files on disk

Root: `%LOCALAPPDATA%\Costas\` (builds before the rename used `Ft8Client\`; the app moves it on first start) (platform equivalent elsewhere).

| Path | Content |
| --- | --- |
| `costas.db` | SQLite database (WAL mode) |
| `settings.json` | App settings and station profiles. No secrets |
| `neighbour-history.json` | Daily summary of the comparison with stations near me, and marked station changes (see below) |
| `logs\app-YYYYMMDD.log` | Serilog files, 14 days |
| `journal\decodes-YYYY-MM.txt` | Decode journal, one line per decode and per transmission |
| `wav\YYMMDD_HHMMSS.wav` | Saved slot audio, per the Files setting, deleted after the retention period |
| `cache\cty.dat` | Updated country file, if newer than the bundled one |
| `tmp\` | Per-run `jt9` scratch folders, cleaned at start |

Secrets (QRZ logbook key per profile, QRZ username and password, HamQTH callsign and password) are stored through an `ISecretStore`: Windows Credential Manager on Windows.

### neighbour-history.json

Written by `NeighbourHistoryStore` (Data) for the Reach tab's "Did a change help?" (`04-domain-logic.md` section 13;
owner approval 2026-10-12). A JSON file rather than a table, so it needs no migration. camelCase:

```json
{ "schema": 1,
  "days":  [ { "day": "2026-10-12", "call": "W7LIT", "band": "20m", "mode": "FT8", "continent": "EU",
               "diffCounts": { "-7": 3, "-6": 1 }, "receiverSum": 14 } ],
  "marks": [ { "day": "2026-10-09", "call": "W7LIT", "note": "new vertical", "createdUtc": "2026-10-09T17:02:00Z" } ] }
```

- One `days` entry per UTC day, call, band, mode and receiver continent. `diffCounts` is how many samples had each
  difference (my median minus the neighbours' median, dB), so the median over any span of days is exact.
  `receiverSum` adds up the shared receivers of each sample.
- `marks`: at most one per call and day; the marked day counts as after the change.
- Kept 370 days. Written whole to `neighbour-history.json.tmp`, then moved over the old file. A missing file is an
  empty history; an unreadable one is copied to `.bad` and the history starts again.

## SQLite schema

Migrations are numbered SQL scripts applied in order; the current version is stored in `PRAGMA user_version`.

```sql
CREATE TABLE qso (
  id               INTEGER PRIMARY KEY,
  profile_id       TEXT    NOT NULL,          -- station profile that made or owns it
  call             TEXT    NOT NULL,
  qso_date_on      TEXT    NOT NULL,          -- UTC ISO 8601
  qso_date_off     TEXT,
  band             TEXT    NOT NULL,          -- ADIF band, e.g. '20m'
  freq_hz          INTEGER,
  mode             TEXT    NOT NULL,          -- ADIF mode: 'FT8', or 'MFSK' for FT4
  submode          TEXT,                      -- 'FT4' when mode = 'MFSK'
  rst_sent         TEXT,
  rst_rcvd         TEXT,
  gridsquare       TEXT,
  name             TEXT,
  qth              TEXT,
  state            TEXT,
  country          TEXT,
  dxcc             INTEGER,                   -- ADIF DXCC code if known
  entity_key       TEXT,                      -- country-file primary prefix, computed locally
  continent        TEXT,
  station_callsign TEXT    NOT NULL,
  my_gridsquare    TEXT,
  tx_pwr           TEXT,
  comment          TEXT,
  confirmed        INTEGER NOT NULL DEFAULT 0, -- 1 if any confirmation field says so
  lotw_confirmed   INTEGER NOT NULL DEFAULT 0, -- 1 if LoTW confirmed it (migration 003)
  lotw_upload_state TEXT   NOT NULL DEFAULT 'none', -- none | uploaded (signed through TQSL)
  need_tier        INTEGER,                   -- tier at the time of the contact (local contacts only)
  source           TEXT    NOT NULL,          -- 'local', 'qrz', 'adif'
  qrz_logid        INTEGER UNIQUE,
  upload_state     TEXT    NOT NULL DEFAULT 'none', -- none, queued, uploaded, failed
  upload_error     TEXT,
  raw_adif         TEXT,                      -- original record for imported or fetched contacts
  created_utc      TEXT    NOT NULL,
  modified_utc     TEXT    NOT NULL
);
CREATE INDEX ix_qso_call        ON qso(call);
CREATE INDEX ix_qso_entity_band ON qso(entity_key, band);
CREATE INDEX ix_qso_grid        ON qso(substr(gridsquare, 1, 4));
CREATE INDEX ix_qso_date        ON qso(qso_date_on);

CREATE TABLE upload_queue (
  id            INTEGER PRIMARY KEY,
  qso_id        INTEGER NOT NULL REFERENCES qso(id) ON DELETE CASCADE,
  target        TEXT    NOT NULL,             -- 'qrz'
  attempts      INTEGER NOT NULL DEFAULT 0,
  next_try_utc  TEXT    NOT NULL,
  last_error    TEXT
);

CREATE TABLE sync_state (
  profile_id    TEXT NOT NULL,
  target        TEXT NOT NULL,                -- 'qrz'
  last_sync_utc TEXT,
  last_logid    INTEGER,
  PRIMARY KEY (profile_id, target)
);

CREATE TABLE callsign_cache (
  call         TEXT PRIMARY KEY,
  name         TEXT,
  city         TEXT,
  state        TEXT,
  country      TEXT,
  grid         TEXT,
  lat          REAL,
  lon          REAL,
  dxcc         INTEGER,
  fetched_utc  TEXT NOT NULL,
  found        INTEGER NOT NULL,              -- 0 caches a miss for 7 days
  -- migration 002: details for the station flyout (the migration empties the cache so rows are refetched in full)
  nickname     TEXT,
  county       TEXT,
  license_class TEXT,                         -- QRZ "class", e.g. E, G, T
  time_zone    TEXT,
  gmt_offset   REAL,                          -- standard offset, hours
  observes_dst INTEGER,
  image_url    TEXT,
  qsl_manager  TEXT,
  lotw         INTEGER,
  eqsl         INTEGER,
  paper_qsl    INTEGER,                       -- QRZ "mqsl"
  cq_zone      INTEGER,
  itu_zone     INTEGER
);

CREATE TABLE spot (                           -- reports of my signal from PSK Reporter
  id            INTEGER PRIMARY KEY,
  my_call       TEXT    NOT NULL,
  rx_call       TEXT    NOT NULL,
  rx_grid       TEXT,
  rx_dxcc       INTEGER,
  rx_entity_key TEXT,
  band          TEXT    NOT NULL,
  mode          TEXT    NOT NULL,
  freq_hz       INTEGER,
  snr           INTEGER,
  time_utc      TEXT    NOT NULL,
  source        TEXT    NOT NULL              -- 'mqtt', 'query'
);
CREATE INDEX ix_spot_band_time ON spot(band, time_utc);
CREATE INDEX ix_spot_rx        ON spot(rx_call, band);

CREATE TABLE band_stat (                      -- per-slot counts for "per slot over the past hour"
  slot_start_utc TEXT NOT NULL,
  band           TEXT NOT NULL,
  mode           TEXT NOT NULL,
  decodes        INTEGER NOT NULL,
  PRIMARY KEY (slot_start_utc, band, mode)
);
```

Duplicate rule for the log (import, sync and local): same `call`, `band`, `mode` (and `submode`) and `qso_date_on` within 30 minutes is the same contact. Prefer the row with a `qrz_logid`; merge missing fields into it.

## LogIndex (in memory)

Built from `qso` for the active profile's callsign; rebuilt after sync or import; updated incrementally on a new local contact.

```
entitiesWorked            : set of entity_key
entitiesWorkedByBand      : band -> set of entity_key
entitiesConfirmedByBand   : band -> set of entity_key
gridsWorked               : set of grid4
gridsWorkedByBand         : band -> set of grid4
callsByBandMode           : (band, modeKey) -> set of call
statesWorkedByBand        : band -> set of US state      (contacts whose entity is the USA)
contactsByCall            : call -> list of (date, band, mode, confirmed)   -- for station details
```

`modeKey` is `FT8` or `FT4` (from mode and submode).

## ADIF mapping

Writer and reader support ADIF 3.1 text (`<name:len>value`, `<eor>`, header ending `<eoh>`).

| ADIF field | From |
| --- | --- |
| `call` | DX callsign |
| `qso_date`, `time_on` | Start, UTC, `YYYYMMDD` and `HHMMSS` |
| `qso_date_off`, `time_off` | End |
| `band` | `20m` style |
| `freq` | MHz with 6 decimals (dial + Tx offset) |
| `mode`, `submode` | `FT8`; or `MFSK` + `FT4` |
| `rst_sent`, `rst_rcvd` | Reports with sign, e.g. `-08` |
| `gridsquare` | DX grid |
| `station_callsign`, `operator` | My call |
| `my_gridsquare` | My grid |
| `tx_pwr` | Watts: the radio's power setting when Hamlib can report it, otherwise the power in Settings |
| `name`, `qth`, `state`, `country`, `dxcc` | From QRZ lookup when available |
| `comment` | Mode and reports, e.g. `FT8 Sent: -01 Rcvd: -13` (Rcvd left out when none was received); the operator can edit it |
| `app_qrzlog_logid` | On records fetched from QRZ |
| `sig`, `sig_info`, `pota_ref` | Park fields. A local contact with a spotted POTA activator gets `sig` = `POTA` and `sig_info` = the park reference. There is no column for them: they are kept in `raw_adif` and copied from it on upload, export and UDP; imported and fetched records keep theirs the same way |

Import must tolerate unknown fields, junk between records and records with no call (skip them and count).

## Decode journal

Plain text, one line per event, monthly files. Compatible in spirit with WSJT-X `ALL.TXT`:

```
260104_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95
260104_025400    14.074 Tx FT8      0  0.0 1650 JA1QRS W7LIT DN40
```

## settings.json

```json
{
  "schema": 1,
  "activeProfile": "home",
  "profiles": [{
    "id": "home", "name": "Home", "callsign": "W7LIT", "grid": "DN40",
    "rig": { "mode": "hamlib", "model": 3073, "port": "COM4", "baud": 115200, "ptt": "cat", "pttPort": null },
    "audio": { "inputId": "...", "outputId": "...", "txGainDb": -6.0 },
    "powerWatts": 25
  }],
  "operating": {
    "band": "20m", "mode": "FT8", "cqParity": "even",
    "retryLimit": 4, "watchdogMinutes": 6, "signoff": "RR73", "autoLog": true,
    "cqModifier": "", "gridInReply": true, "answerSignoff": "73", "freeTextAfter": "",
    "txOffset": "auto", "lateStartLimitMs": 1000, "pttLeadMs": 200
  },
  "ranking": {
    "tierOrder": ["country", "band", "grid", "call"],
    "preferHearsMe": true, "onlyCallingCq": true, "hideWorked": true, "skipLongShots": false,
    "confirmedOnly": false, "confirmedBy": "lotw"
  },
  "lotw": { "enabled": false, "username": "", "downloadHours": 12, "tqslPath": null, "stationLocation": "",
            "uploadOnComplete": false, "userList": true },
  "hunt": { "targets": [{ "kind": "country", "value": "KL", "band": "40m" }], "watchAllBands": true,
            "nearbyKm": 1000, "sound": true },
  "qrz": { "logbookEnabled": true, "lookupEnabled": true, "hamQthEnabled": false, "syncMinutes": 15, "uploadOnComplete": true },
  "pskReporter": { "feedEnabled": true, "uploadSpots": false },
  "udp": { "enabled": true, "address": "127.0.0.1", "port": 2237 },
  "files": { "saveWav": "none", "retentionDays": 30 },
  "appearance": { "theme": "system", "units": "miles" },
  "paths": { "jt9": null, "hamlibDir": null }
}
```

The rig model number shown is an example; always take it from the Hamlib model list.

## Frequency table

`frequencies.json`, shipped with the app and editable: a list of `{ band, mode, dialHz, lowHz, highHz }`. `lowHz` and `highHz` are the allowed transmit range for the band-edge guard. The defaults are in `01-product-spec.md`.
