# PSK Reporter samples

- `mqtt-messages.jsonl` — feed payloads in the shape documented at https://www.mqtt.pskreporter.info/ (field names `sq`, `f`,
  `md`, `rp`, `t`, `sc`, `sl`, `rc`, `rl`, `sa`, `ra`, `b`). Example data: one duplicate sequence number, one report for another
  sender, one malformed line. The broker could not be reached from the build environment, so these were not captured live.
- `query-reply.xml` — a query service reply in the documented `<receptionReports>` shape.
