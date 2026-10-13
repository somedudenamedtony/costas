#!/usr/bin/env bash
# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Builds the Linux preview: publishes the app self-contained for linux-x64 and packs it as a tarball. jt9, rigctld and
# PortAudio come from the distribution (wsjtx, libhamlib-utils, libportaudio2); the app finds them on the PATH.
# Output: artifacts/Costas-<version>-linux-x64.tar.gz and its .sha256
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
[ -n "${1:-}" ] && version="$version.$1"
name="Costas-$version-linux-x64"
art="$ROOT/artifacts"
stage="$art/$name"
rm -rf "$stage" "$art/$name.tar.gz" "$art/$name.tar.gz.sha256"
mkdir -p "$stage"

dotnet publish src/Ft8Client.App -c Release -r linux-x64 --self-contained -o "$stage" "-p:Version=$version"
rm -f "$stage"/*.xml "$stage"/*.pdb
cp LICENSE "$stage/"
cp packaging/linux/README.txt packaging/linux/costas.desktop "$stage/"
cp src/Ft8Client.App/Assets/costas-256.png "$stage/costas.png"
[ -x "$stage/Costas" ] || { echo "Costas missing from the publish output" >&2; exit 1; }

tar -C "$art" -czf "$art/$name.tar.gz" "$name"
(cd "$art" && sha256sum "$name.tar.gz" | cut -d' ' -f1 > "$name.tar.gz.sha256")
echo "$art/$name.tar.gz"
