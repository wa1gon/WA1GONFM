#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet publish src/WA1GONFM.App/WA1GONFM.App.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o dist/linux-x64
printf '
Single-file binary: dist/linux-x64/WA1GONFM.App
'
