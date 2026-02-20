#!/usr/bin/env bash
set -euo pipefail

# Run the server for DnF Test with SQLite fallback and test assumptions
export ACE_TEST_USE_SQLITE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "Starting DnF Test server (SQLite fallback enabled)..."
DOTNET_PROJECT="Source/ACE.Server/ACE.Server.csproj"
if [ ! -f "$DOTNET_PROJECT" ]; then
  echo "Project not found: $DOTNET_PROJECT"
  echo "Have you staged files? Try running ./dnf_deploy.sh /opt/dnftest on the test host."
  exit 1
fi

# Run in foreground
exec dotnet run --project "$DOTNET_PROJECT" --no-build --verbosity minimal "$@"
