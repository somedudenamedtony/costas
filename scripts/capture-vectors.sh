#!/usr/bin/env bash
# Captures encoder reference vectors from WSJT-X's ft8code and ft4code into tests/Ft8Client.Core.Tests/Vectors.
# Columns: message, decoded text as printed by the tool, i3.n3, 77 message bits, channel symbols.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
OUT="$ROOT/tests/Ft8Client.Core.Tests/Vectors"
mapfile -t MSGS < "$OUT/messages.txt"
for tool in ft8code ft4code; do
  dest="$OUT/$tool.tsv"
  echo "# $tool vectors. $("$(dirname "$(command -v $tool)")"/wsjtx_app_version -v 2>/dev/null || echo unknown)" > "$dest"
  for m in "${MSGS[@]}"; do
    [ -z "$m" ] && continue
    "$tool" "$m" | python3 -c '
import sys,re
lines=sys.stdin.read().splitlines()
msg=sys.argv[1]
row=[l for l in lines if re.match(r"\s*1\.\s",l)][0]
# Fixed columns: message (37 chars from col 5), decoded (37), then err flag and i3.n3
dec=row[42:79].strip() if len(row)>42 else ""
m=re.search(r"(\d)\.(\d)?\s",row[79:]+" ")
i3n3=re.search(r"\b(\d\.\d?)\s+[A-Z]",row[79:]).group(1) if re.search(r"\b(\d\.\d?)\s+[A-Z]",row[79:]) else "?"
bits=lines[[i for i,l in enumerate(lines) if "77 bits" in l][0]+1].strip()
si=[i for i,l in enumerate(lines) if l.startswith("Channel symbols")][0]
sym=re.sub(r"[^0-9]","",lines[si+2])
print("\t".join([msg,dec,i3n3,bits,sym]))
' "$m" >> "$dest"
  done
  echo "$dest: $(grep -vc '^#' "$dest") vectors"
done
