#!/usr/bin/env bash
# Adds the GPLv3 short header to C# files that lack it, or with --check, lists them and fails.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
HEADER="$ROOT/scripts/header.txt"
first=$(head -1 "$HEADER")
missing=0
while IFS= read -r -d '' f; do
  if [ "$(head -1 "$f" | sed 's/^\xEF\xBB\xBF//')" != "$first" ]; then
    if [ "${1:-}" = "--check" ]; then echo "missing header: ${f#$ROOT/}"; missing=1
    else { cat "$HEADER"; sed '1s/^\xEF\xBB\xBF//' "$f"; } > "$f.tmp" && mv "$f.tmp" "$f"; fi
  fi
done < <(find "$ROOT/src" "$ROOT/tests" "$ROOT/tools" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0)
exit $missing
