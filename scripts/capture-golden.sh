#!/usr/bin/env bash
# Captures golden decoder output for each sample WAV by running jt9 directly with the
# same options Jt9Decoder uses. Re-run only when the pinned jt9 version changes on purpose.
# Usage: scripts/capture-golden.sh [path/to/jt9]
set -euo pipefail
JT9=${1:-$(command -v jt9 || echo third_party/wsjtx/bin/jt9)}
ROOT=$(cd "$(dirname "$0")/.." && pwd)
VERSION=$("$(dirname "$JT9")/wsjtx_app_version" -v 2>/dev/null || echo unknown)

# Per-sample decode context (my call, grid, dx call, qso progress). Default: none.
declare -A CTX=(
  [260101_000030]="-c W7LIT -G DN40"
  [260101_000045]="-c W7LIT -G DN40"
  [260101_000100]="-c W7LIT -G DN40 -x K1ABC -Q 1"
)

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
for wav in "$ROOT"/samples/ft8/*.wav "$ROOT"/samples/ft4/*.wav; do
  name=$(basename "$wav" .wav)
  case "$wav" in */ft4/*) mode=(-5 -p 7) ;; *) mode=(-8) ;; esac
  ctx=${CTX[$name]:-}
  q=""
  # shellcheck disable=SC2086
  set -- $ctx
  # Mirror Jt9Decoder.BuildArguments ordering.
  args=("${mode[@]}" -d 3 -L 200 -H 3000 -f 1500)
  while [ $# -gt 0 ]; do
    case "$1" in -Q) q="$2"; shift 2 ;; *) args+=("$1" "$2"); shift 2 ;; esac
  done
  [ -n "$q" ] && args+=(-Q "$q")
  cp "$wav" "$work/"
  out="${wav%.wav}.golden.txt"
  {
    echo "# jt9 golden output. $VERSION"
    echo "# args: $ctx"
    (cd "$work" && "$JT9" "${args[@]}" -e "$(dirname "$JT9")" -a "$work" -t "$work" "$(basename "$wav")")
  } > "$out"
  echo "$out: $(grep -c -v '^[#<]' "$out") decodes"
  rm -f "$work"/*
done
