-- Keep eQSL delivery separate from QRZ delivery.
ALTER TABLE qso ADD COLUMN eqsl_upload_state TEXT NOT NULL DEFAULT 'none';
ALTER TABLE qso ADD COLUMN eqsl_upload_error TEXT;
ALTER TABLE qso ADD COLUMN eqsl_confirmed INTEGER NOT NULL DEFAULT 0;
CREATE TABLE eqsl_inbox (
  profile_id TEXT NOT NULL,
  card_key TEXT NOT NULL,
  raw_adif TEXT NOT NULL,
  received_utc TEXT NOT NULL,
  PRIMARY KEY (profile_id, card_key)
);
