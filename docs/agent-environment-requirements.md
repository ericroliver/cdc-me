# Agent Container Environment Requirements

> **Purpose:** This document defines the tooling and environment requirements for the dtai-sword agent container. Since the container is ephemeral, all installations are lost on restart. This document should be used by DevOps to bake these requirements into the container image.

## Current Situation

The agent container (`dtai-sword`) runs on Ubuntu 24.04 (arm64) and has:
- .NET 10.0.9 **runtime only** (no SDK)
- `git`, `curl`, `ssh` available
- No `rg` (ripgrep), `jq`, `wget`, or `docker`
- No dotnet global tools installed
- No persistent PATH configuration for dotnet

During initial setup, the agent had to:
1. Download and install the .NET 10 SDK via `dotnet-install.sh` (~200MB download, ~30s)
2. Manually `export PATH="/usr/share/dotnet:$PATH"` in every shell session
3. Fall back to `grep` instead of `rg` for code search
4. Skip `dotnet-outdated-tool` checks in the lint script

## Required: .NET SDK

**Current state:** Runtime only (10.0.9), no SDK.

**Requirement:** Install the .NET SDK matching the project's target framework.

| Project Target | SDK Required | Notes |
|---|---|---|
| `net10.0` (after upgrade) | .NET 10 SDK | The container already has the 10.0.9 runtime. Install the matching SDK. |
| `net10.0` (current) | .NET 10 SDK | Currently installed ad-hoc via `dotnet-install.sh`. Will be obsolete after upgrade. |

**Installation:**
```bash
# Option A: Via dotnet-install.sh (what we currently do ad-hoc)
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0.1xx --install-dir /usr/share/dotnet

# Option B: Via apt (preferred for image baking)
# Add Microsoft package repo first, then:
apt-get install -y dotnet-sdk-10.0
```

**PATH configuration:**
The SDK must be on PATH permanently. Add to `/etc/profile.d/dotnet.sh`:
```bash
export PATH="/usr/share/dotnet:$PATH"
export DOTNET_ROOT="/usr/share/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
```

Source this in `~/.bashrc` or ensure the shell profile picks it up:
```bash
echo 'source /etc/profile.d/dotnet.sh' >> ~/.bashrc
```

## Required: Development Tools

### ripgrep (`rg`)

**Why:** The agent uses `rg` for fast, gitignore-aware code search. It's significantly faster than `grep -r` and respects `.gitignore` rules out of the box. The `analyze` extension and standard development workflows expect it.

```bash
apt-get install -y ripgrep
```

### jq

**Why:** Parsing JSON output from `dotnet` CLI, `docker` commands, API responses, and configuration files. Many workflows produce JSON that needs to be inspected programmatically.

```bash
apt-get install -y jq
```

### wget

**Why:** The project's lint-check script and Docker health checks reference `wget`. The Dockerfile installs it in the container image, but the agent container itself should have it for testing API endpoints and health checks.

```bash
apt-get install -y wget
```

## Required: .NET Global Tools

### dotnet-outdated-tool

**Why:** The project's `scripts/lint-check.sh` references `dotnet-outdated-tool` for checking outdated packages. Currently produces a warning instead of running. The CI pipeline (`ci.yml`) also installs and runs it.

```bash
dotnet tool install -g dotnet-outdated-tool
```

### dotnet-format (built into SDK)

**Note:** `dotnet format` is included with the .NET SDK starting from .NET 9. No separate installation needed. Just ensure the SDK is installed.

## Optional: Nice-to-Have Tools

### Docker CLI (client only)

**Why:** While the agent cannot run Docker-in-Docker (as noted in `agents.md`), having the Docker CLI client would allow:
- Inspecting remote containers via `DOCKER_HOST`
- Building images on remote Docker hosts
- Managing Docker Compose on deploy targets (e.g., `blue`)

```bash
apt-get install -y docker-ce-cli docker-compose-plugin
```

**Note:** This requires the Docker apt repository. The agent would use `DOCKER_HOST=ssh://eo@blue` or similar to manage remote Docker.

### SSH client configuration

**Why:** The agent needs SSH access to homelab hosts (`blue`, `m3x`, `truenas`, `jetkvm`, `afterburner`). Pre-configuring SSH known hosts and key paths would save setup time.

Ensure `ssh` is installed (currently available) and consider pre-populating `~/.ssh/known_hosts` for:
- `blue.local` / `100.114.45.33`
- `m3x.local` / `100.107.242.12`
- `truenas` / `192.168.86.23`

### unzip / tar

**Why:** The CI pipeline produces `.zip` and `.tar.gz` artifacts. Having `unzip` available is useful for inspecting build artifacts.

```bash
apt-get install -y unzip
```

## Environment Variables

These should be set in the container's environment or `/etc/profile.d/`:

```bash
# .NET
export PATH="/usr/share/dotnet:$PATH"
export DOTNET_ROOT="/usr/share/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# .NET tools (global tools install here by default)
export PATH="$HOME/.dotnet/tools:$PATH"
```

## Summary Checklist for DevOps

| Item | Status | Priority | Installation |
|---|---|---|---|
| .NET 10 SDK | ❌ Missing (runtime only) | **Critical** | `dotnet-install.sh --channel 10.0.1xx` or `apt-get install dotnet-sdk-10.0` |
| PATH for dotnet | ❌ Not persisted | **Critical** | `/etc/profile.d/dotnet.sh` |
| ripgrep (`rg`) | ❌ Missing | **High** | `apt-get install ripgrep` |
| jq | ❌ Missing | **High** | `apt-get install jq` |
| wget | ❌ Missing | **Medium** | `apt-get install wget` |
| dotnet-outdated-tool | ❌ Missing | **Medium** | `dotnet tool install -g dotnet-outdated-tool` |
| unzip | ❌ Missing (assumed) | **Low** | `apt-get install unzip` |
| Docker CLI (client) | ❌ Missing | **Low** (optional) | `apt-get install docker-ce-cli docker-compose-plugin` |
| SSH known_hosts | ⚠️ Not pre-populated | **Low** (nice-to-have) | Pre-populate for homelab hosts |

## Verification Commands

After baking the image, these commands should all succeed:

```bash
dotnet --version                    # Should print 10.0.x
rg --version                        # Should print ripgrep version
jq --version                       # Should print jq version
wget --version                      # Should print wget version
dotnet tool list -g                 # Should list dotnet-outdated-tool
echo $PATH | grep dotnet            # Should contain /usr/share/dotnet
```
