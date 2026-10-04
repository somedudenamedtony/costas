#!/usr/bin/env bash
# Links jt9, ft8code, ft4code and rigctld from the distribution packages into third_party/.
# Debian/Ubuntu: sudo apt-get install wsjtx libhamlib-utils
set -euo pipefail
ROOT=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$ROOT/wsjtx/bin" "$ROOT/hamlib/bin"
for t in jt9 ft8code ft4code ft8sim wsjtx_app_version; do
  p=$(command -v "$t" || true)
  [ -n "$p" ] && ln -sf "$p" "$ROOT/wsjtx/bin/$t" || echo "warning: $t not found (install the wsjtx package)"
done
for t in rigctld rigctl; do
  p=$(command -v "$t" || true)
  [ -n "$p" ] && ln -sf "$p" "$ROOT/hamlib/bin/$t" || echo "warning: $t not found (install libhamlib-utils)"
done
curl -fsSL -o "$ROOT/cty.dat" https://www.country-files.com/cty/cty.dat || echo "warning: cty.dat download failed"
echo "done"
