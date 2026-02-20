#!/usr/bin/env bash
set -euo pipefail

# dnf_deploy.sh
# Stages repository files into a target directory on Linux and starts the test server.
# Usage: sudo ./dnf_deploy.sh /opt/dnftest

TARGET_DIR=${1:-/opt/dnftest}
REPO_ROOT=$(pwd)

echo "Staging DnF test into: ${TARGET_DIR}"

if [ ! -d "${REPO_ROOT}" ]; then
  echo "Run this script from the repository root." >&2
  exit 1
fi

# create target
sudo mkdir -p "${TARGET_DIR}"
# copy necessary files (dotnet project + start scripts + Config example)
sudo rsync -av --delete --exclude '.git' \
  "${REPO_ROOT}/Source/" "${TARGET_DIR}/Source/"

# copy top-level scripts
sudo install -m 0755 "${REPO_ROOT}/dnftest.sh" "${TARGET_DIR}/dnftest.sh" || true
sudo install -m 0755 "${REPO_ROOT}/start.sh" "${TARGET_DIR}/start.sh" || true

# ensure Config.js exists: if not, try to copy Config.js.example
if [ ! -f "${TARGET_DIR}/Config.js" ]; then
  if [ -f "${REPO_ROOT}/Config.js.example" ]; then
    sudo cp "${REPO_ROOT}/Config.js.example" "${TARGET_DIR}/Config.js"
    echo "Config.js created from Config.js.example"
  else
    echo "Warning: Config.js not found; please create ${TARGET_DIR}/Config.js from example or env vars." >&2
  fi
fi

# set ownership for runtime user (use current user if not root)
USER_TO_OWN=${SUDO_USER:-$(whoami)}
sudo chown -R ${USER_TO_OWN}:${USER_TO_OWN} "${TARGET_DIR}"

# make scripts executable
chmod +x "${TARGET_DIR}/dnftest.sh" || true
chmod +x "${TARGET_DIR}/start.sh" || true

cat <<EOF
Staging complete.
To run the test server on this host:
  cd ${TARGET_DIR}
  ./start.sh

Or to run directly (background):
  cd ${TARGET_DIR}
  nohup ./start.sh > server.log 2>&1 &
EOF
