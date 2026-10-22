#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "Publishing filepilot for linux-x64..."

dotnet publish src/WA1GONFM.App/WA1GONFM.App.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o dist/linux-x64

echo "Published: $(pwd)/dist/linux-x64/filepilot"
