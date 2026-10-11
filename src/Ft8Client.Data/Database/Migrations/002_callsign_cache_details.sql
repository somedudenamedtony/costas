-- QRZ lookup: more fields for the station details (county, licence class, local time, QSL, zones, photo).
ALTER TABLE callsign_cache ADD COLUMN nickname TEXT;
ALTER TABLE callsign_cache ADD COLUMN county TEXT;
ALTER TABLE callsign_cache ADD COLUMN license_class TEXT;
ALTER TABLE callsign_cache ADD COLUMN time_zone TEXT;
ALTER TABLE callsign_cache ADD COLUMN gmt_offset REAL;
ALTER TABLE callsign_cache ADD COLUMN observes_dst INTEGER;
ALTER TABLE callsign_cache ADD COLUMN image_url TEXT;
ALTER TABLE callsign_cache ADD COLUMN qsl_manager TEXT;
ALTER TABLE callsign_cache ADD COLUMN lotw INTEGER;
ALTER TABLE callsign_cache ADD COLUMN eqsl INTEGER;
ALTER TABLE callsign_cache ADD COLUMN paper_qsl INTEGER;
ALTER TABLE callsign_cache ADD COLUMN cq_zone INTEGER;
ALTER TABLE callsign_cache ADD COLUMN itu_zone INTEGER;
-- Entries cached before these columns existed lack them; drop them so the next lookup fetches the full record.
DELETE FROM callsign_cache;
