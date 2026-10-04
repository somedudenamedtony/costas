#!/usr/bin/env bash
# CI entry point: header check, build (warnings are errors), tests.
set -euo pipefail
cd "$(dirname "$0")/.."
scripts/headers.sh --check
dotnet build -c Release
dotnet test -c Release --no-build
