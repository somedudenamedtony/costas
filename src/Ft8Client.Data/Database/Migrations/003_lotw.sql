-- LoTW: which contacts LoTW confirms (what DXCC and WAS credit) and which have been signed and uploaded through TQSL.
ALTER TABLE qso ADD COLUMN lotw_confirmed INTEGER NOT NULL DEFAULT 0;
ALTER TABLE qso ADD COLUMN lotw_upload_state TEXT NOT NULL DEFAULT 'none';
-- Contacts fetched or imported before: their ADIF already says whether LoTW confirmed them, or whether they were uploaded.
UPDATE qso SET lotw_confirmed = 1 WHERE raw_adif LIKE '%<lotw_qsl_rcvd:1>Y%' OR raw_adif LIKE '%<lotw_qsl_rcvd:1>V%';
UPDATE qso SET lotw_upload_state = 'uploaded' WHERE raw_adif LIKE '%<lotw_qsl_sent:1>Y%';
