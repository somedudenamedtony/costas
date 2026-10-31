-- Edits and deletes made in the Log view. A later QRZ sync must not undo them: an edited contact keeps its own fields
-- (QRZ still adds confirmations), and a deleted one is not brought back by QRZ or LoTW.
ALTER TABLE qso ADD COLUMN edited_utc TEXT;
CREATE TABLE qso_deleted (
  id               INTEGER PRIMARY KEY,
  profile_id       TEXT    NOT NULL,
  call             TEXT    NOT NULL,
  band             TEXT    NOT NULL,
  mode             TEXT    NOT NULL,
  submode          TEXT,
  qso_date_on      TEXT    NOT NULL,
  station_callsign TEXT    NOT NULL,
  qrz_logid        INTEGER,
  deleted_utc      TEXT    NOT NULL
);
CREATE INDEX ix_qso_deleted_call ON qso_deleted(call);
