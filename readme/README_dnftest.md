Linux DnF Test Run

1) Stage files to test host (example):

sudo ./dnf_deploy.sh /opt/dnftest

2) On test host, run server:

cd /opt/dnftest
./start.sh

Notes:
- Scripts set `ACE_TEST_USE_SQLITE=1` so tests and local runs use SQLite fallback.
- Ensure `Config.js` exists at the target; `dnf_deploy.sh` will copy `Config.js.example` if present.
- Make scripts executable: `chmod +x dnftest.sh start.sh dnf_deploy.sh`.
