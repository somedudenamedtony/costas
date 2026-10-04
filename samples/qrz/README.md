# QRZ Logbook API samples

Hand-written fixtures that reproduce the shape of real QRZ Logbook API responses observed on 2026-10-04
(V6 in docs/08-open-questions.md). They contain no real contact data.

- `status.txt` — `ACTION=STATUS` reply. Keys come in no fixed order.
- `fetch-page.txt` — `ACTION=FETCH` reply. `RESULT` and `COUNT` come first and `ADIF=` is last; the ADIF text is
  HTML-entity encoded (`&lt;call:5&gt;K1ABC`, and `&amp;` inside values), so the reply cannot be split on `&`.
  Records arrive in ascending `app_qrzlog_logid`. FT4 contacts come back as `mode` `FT4` (not `MFSK`/`FT4`).
  Confirmation is `app_qrzlog_status` `C`; `qsl_rcvd` and `lotw_qsl_rcvd` are also read.
- `auth-fail.txt` — reply to a wrong key: `STATUS=AUTH&RESULT=AUTH&REASON=...`.
- An empty page (paged past the last record, or `MODSINCE` with nothing newer) is `RESULT=FAIL&COUNT=0` with no reason; treat it as the end, not an error.
- `TYPE:LOGIDS` returns `RESULT=OK&COUNT=3&LOGIDS=1,2,3`.
