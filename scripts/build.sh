#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$ROOT/dist"
cd "$ROOT"
go mod download
go test ./...
CGO_ENABLED=0 go build -trimpath -ldflags '-s -w' -o "$ROOT/dist/nxkeys" ./cmd/nxkeys
cp "$ROOT/config/nx2512-pro-hybrid.json" "$ROOT/dist/nx2512-pro-hybrid.json"
echo "Built $ROOT/dist/nxkeys"
