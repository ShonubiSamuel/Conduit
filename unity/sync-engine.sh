#!/usr/bin/env bash
# Rebuild the headless engine and vendor its DLL into the Unity project's Plugins folder.
# Run this whenever Conduit.Engine changes, then let Unity reimport.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet build "$root/src/Conduit.Engine" -c Release
cp "$root/src/Conduit.Engine/bin/Release/netstandard2.1/Conduit.Engine.dll" \
   "$root/unity/ConduitDesktop/Assets/Plugins/Conduit.Engine.dll"
echo "synced Conduit.Engine.dll → unity/ConduitDesktop/Assets/Plugins/"
