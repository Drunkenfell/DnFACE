# .NET 10 Migration Checklist — DnFACE / Pegasus / Mods

This checklist documents the steps to migrate the codebase and mods to .NET 10 and to install .NET 10 on two Linux test servers. Use the checkboxes to mark progress.

---

## Branching & preflight
- [ ] Create a dedicated migration branch from `master`: `feature/dotnet10-migration`.
- [ ] Ensure working tree is clean (stash or commit local WIP).
- [ ] Back up current `master` and `harmony+` branches (push tags or create `master-before-dotnet10` tag).

Commands (bash):

```bash
# from repo root
git fetch origin --prune
git checkout master
git pull --ff-only
git checkout -b feature/dotnet10-migration
# tag backup
git tag -a master-before-dotnet10 -m "Backup before .NET 10 migration" origin/master
git push origin master-before-dotnet10
```

---

## Inventory (automated)
- [ ] Produce a project & dependency inventory CSV.

Commands to run from repo root (bash):

```bash
# list all csproj files
find . -name "*.csproj" | sed 's|\./||' > projects.txt

# for each project, capture TargetFramework and package list
printf "Project,TargetFramework,Packages\n" > project_inventory.csv
while read p; do
  tf=$(xmllint --xpath "string(//TargetFramework)" "$p" 2>/dev/null || echo "")
  pkgs=$(dotnet list "$p" package --verbosity quiet 2>/dev/null | sed -n '1,200p' | tr '\n' ';' | sed 's/;/\\n/g' | sed ':a;N;$!ba;s/\n/:/g')
  printf "%s,%s,%s\n" "$p" "$tf" "$pkgs" >> project_inventory.csv
done < projects.txt
```

(If `xmllint` not available, `grep` the `<TargetFramework>` line.)

- [ ] Identify native/OS-specific dependencies (MySQL native libs, PKCS#11, or PInvoke usage). Run:

```bash
grep -RIn "DllImport\|PInvoke\|native" Source || true
```

- [ ] Note any Harmony/HarmonyLib usage, reflection-heavy code, and other runtime-sensitive libraries.

---

## Prepare SDK on build/CI and developers
- [ ] Agree on .NET SDK patch version (e.g., `10.0.100`).
- [ ] Add `global.json` to repo root pinned to the chosen SDK version.

Example `global.json`:

```json
{
  "sdk": {
    "version": "10.0.100"
  }
}
```

- [ ] Update CI images (GitHub Actions / Azure / TeamCity) to use .NET 10 SDK image or install SDK on runners.

---

## Install .NET 10 on Linux test servers (both servers)
- [ ] Identify distro/version on each test server:

```bash
cat /etc/os-release
uname -a
```

- [ ] Install Microsoft package feed and .NET 10 SDK/runtime. Replace `<OS-PATH>` below with appropriate distro path (ubuntu/22.04, ubuntu/24.04, centos/8, fedora/...).

Ubuntu / Debian example:

```bash
# run as root or sudo
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y apt-transport-https
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# verify
dotnet --info
dotnet --list-sdks
```

RHEL / CentOS / Fedora example:

```bash
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
sudo dnf install -y dotnet-sdk-10.0
# or if using yum
# sudo yum install -y dotnet-sdk-10.0

# verify
dotnet --info
```

Alpine / other: use official Microsoft guidance or containers (preferred to avoid porting native binaries).

Notes:
- Prefer installing the SDK on build CI and at least the Runtime (or SDK) on test hosts.
- Use `dotnet --info` to verify the SDK and runtime families.

---

## One-project pilot (proof-of-concept)
- [ ] Pick `ACE.Server` as the pilot.
- [ ] Update `Source/ACE.Server/ACE.Server.csproj` `TargetFramework` to `net10.0`.
- [ ] Restore and build locally and on CI: `dotnet restore`, `dotnet build`.
- [ ] Fix compile errors; update package references on a per-package basis.

Commands (from repo root):

```bash
# make a branch for pilot
git checkout -b pilot/dotnet10-ace-server
# edit csproj: <TargetFramework>net10.0</TargetFramework>
# restore & build
cd Source/ACE.Server
dotnet restore
dotnet build -clp:Summary
```

---

## NuGet package strategy
- [ ] Update major frameworks first: EF Core, Pomelo MySql.EntityFrameworkCore, Microsoft.Data.Sqlite, HarmonyLib, log4net, Serilog, etc.
- [ ] Upgrade packages in small batches (pilot -> repo) and run tests between changes.
- [ ] If a provider lacks .NET 10 support, consider replacing it or pinning that component to run in a separate process.

Commands to update package (example):

```bash
# inside a project
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0  # pick compatible version
# or use nuget/dotnet-outdated tools for a report
```

---

## Reflection / Harmony / Mod loader considerations
- [ ] Audit all uses of `Assembly.Load`, `Assembly.LoadFrom`, `Type.GetType`, and dynamic emit.
- [ ] Test Harmony patches — runtime behavior and patch targets may change.
- [ ] For each mod assembly, rebuild against `net10.0` and retest dynamic patching.

---

## CI, Docker, and images
- [ ] Update Dockerfiles to `mcr.microsoft.com/dotnet/aspnet:10.0` or `mcr.microsoft.com/dotnet/runtime:10` base images where applicable.
- [ ] Update build images in CI workflows to `mcr.microsoft.com/dotnet/sdk:10.0`.
- [ ] Run CI, iterate on failures, and pin working images in `ci/` manifests.

---

## Tests & staging
- [ ] Run unit tests with SQLite fallback: `ACE_TEST_USE_SQLITE=1 dotnet test`.
- [ ] Add integration test that seeds `content_unlocks` and verifies projected XP table monotonicity and level-up behavior.
- [ ] Stage the built `master` to the Linux test hosts using `dnf_deploy.sh` and run `./start.sh`.

Smoke-checks after startup:

```bash
# on test host
ps aux | grep ACE.Server
curl -sS --connect-timeout 2 http://127.0.0.1:PORT/health || true
# or verify listening socket with ss/netstat
ss -ltnp | grep dotnet
```

---

## Rollout & rollback
- [ ] Gate production deploy behind CI passing and manual approval.
- [ ] Rollout to DnF Test, verify, then schedule production rollout.
- [ ] Rollback option: redeploy `master-before-dotnet10` tag or previous Docker image.

Commands to rollback:

```bash
# on host (if using git deployments)
cd /opt/dnftest
git fetch --all --tags
git checkout master-before-dotnet10
./start.sh
```

---

## Post-migration tasks
- [ ] Update docs and developer onboarding (SDK requirement, VS/VSCode settings).
- [ ] Update docker-compose and orchestration manifests.
- [ ] Notify downstream mod authors (Pegasus, DnFACE mods) about target frameworks and required package updates.

---

## Quick verification checklist (run after migration)
- [ ] `dotnet --info` on build and test hosts show .NET 10 SDK/runtime.
- [ ] `dotnet build` completes for pilot project.
- [ ] `dotnet test` passes for unit tests (SQLite fallback enabled).
- [ ] Server starts on test host and responds to basic commands.
- [ ] Harmony patches apply and mod behaviors are verified in test gameplay.


---

If you want, I can:
- Create the `feature/dotnet10-migration` branch now and add `global.json`.
- Perform the automated inventory (project list + packages) in this workspace and commit the `project_inventory.csv` output.
- Generate an automated installation script for .NET 10 for your specific Linux distro versions (provide server OS details and I'll tailor it).

Which of the above would you like me to do next?