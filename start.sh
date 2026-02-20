#!/usr/bin/env bash
set -euo pipefail

# Convenience start script. Use with: sh start.sh
export ACE_TEST_USE_SQLITE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "Running ACE server (test mode)..."
exec dotnet run --project Source/ACE.Server/ACE.Server.csproj
