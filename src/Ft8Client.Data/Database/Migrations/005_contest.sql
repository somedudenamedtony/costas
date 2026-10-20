-- Contest contacts: which contest (ADIF CONTEST_ID) and the exchanges sent and received (STX_STRING, SRX_STRING).
ALTER TABLE qso ADD COLUMN contest_id TEXT;
ALTER TABLE qso ADD COLUMN stx_string TEXT;
ALTER TABLE qso ADD COLUMN srx_string TEXT;
