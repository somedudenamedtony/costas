CREATE TABLE qso (
  id               INTEGER PRIMARY KEY,
  profile_id       TEXT    NOT NULL,
  call             TEXT    NOT NULL,
  qso_date_on      TEXT    NOT NULL,
  qso_date_off     TEXT,
  band             TEXT    NOT NULL,
  freq_hz          INTEGER,
  mode             TEXT    NOT NULL,
  submode          TEXT,
  rst_sent         TEXT,
  rst_rcvd         TEXT,
  gridsquare       TEXT,
  name             TEXT,
  qth              TEXT,
  state            TEXT,
  country          TEXT,
  dxcc             INTEGER,
  entity_key       TEXT,
  continent        TEXT,
  station_callsign TEXT    NOT NULL,
  my_gridsquare    TEXT,
  tx_pwr           TEXT,
  comment          TEXT,
  confirmed        INTEGER NOT NULL DEFAULT 0,
  need_tier        INTEGER,
  source           TEXT    NOT NULL,
  qrz_logid        INTEGER UNIQUE,
  upload_state     TEXT    NOT NULL DEFAULT 'none',
  upload_error     TEXT,
  raw_adif         TEXT,
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
  target        TEXT    NOT NULL,
  attempts      INTEGER NOT NULL DEFAULT 0,
  next_try_utc  TEXT    NOT NULL,
  last_error    TEXT
);

CREATE TABLE sync_state (
  profile_id    TEXT NOT NULL,
  target        TEXT NOT NULL,
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
  found        INTEGER NOT NULL
);

CREATE TABLE spot (
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
  source        TEXT    NOT NULL
);
CREATE INDEX ix_spot_band_time ON spot(band, time_utc);
CREATE INDEX ix_spot_rx        ON spot(rx_call, band);

CREATE TABLE band_stat (
  slot_start_utc TEXT NOT NULL,
  band           TEXT NOT NULL,
  mode           TEXT NOT NULL,
  decodes        INTEGER NOT NULL,
  PRIMARY KEY (slot_start_utc, band, mode)
);
