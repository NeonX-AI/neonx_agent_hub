# NeonX Agent Hub

NeonX Agent Hub is a desktop catalog for installing and opening local AI agents.

The Windows release currently includes OpenClaw `2026.9.1` and installs the `@openclaw/codex` plugin `2026.9.1` by default. On startup it checks whether the required components are available and displays the appropriate action:

- **Install** when OpenClaw is not installed.
- **Install Codex** when OpenClaw is already installed but its Codex plugin is missing.
- **Onboard** when OpenClaw is installed but has not been configured yet; this opens the interactive setup wizard in a visible terminal, then launches the dashboard automatically.
- **Open** when OpenClaw is configured; this starts the local gateway with a freshly generated secure token and launches the authenticated Control UI in the default browser.
- **Stop** terminates the OpenClaw gateway started by NeonX and requests any managed OpenClaw gateway service to stop.
- **Refresh** to run system detection again.

The NeonX UI and multi-resolution Windows executable icon preserve the transparent background and full quality from `assets\images\logo\logo-light.png`. The OpenClaw card uses `assets\images\logo\openclaw.png`.

## Repository layout

```text
assets/
  images/logo/            Shared NeonX branding
config/
  versions.env            Pinned component versions and installer checksums
platforms/
  windows/                Windows source, manifest, icon generation, and build
  macos/                  Reserved for the future macOS app
dist/
  windows/                Generated Windows release
build.ps1                 Platform-aware root build entry point
```

## Windows features

- NeonX agent catalog interface.
- Automatic OpenClaw installation detection.
- Install/Onboard/Open button based on system state.
- OpenClaw Control UI launch through a NeonX-managed local gateway with token authentication.
- Automatic OpenClaw shutdown when the NeonX Hub closes, preventing an unnoticed background gateway from continuing to run.
- System Node.js compatibility detection through `node --version`; NeonX never downloads a local Node.js runtime.
- Incompatible or missing Node.js changes the action to **Upgrade Node** or **Install Node**.
- NeonX explains the OpenClaw compatibility requirement, downloads the official Node.js 26 MSI, verifies SHA-256, requests Administrator permission, and installs Node system-wide.
- OpenClaw detection through `openclaw --version`.
- OpenClaw `2026.9.1` installation through the existing system `npm` command.
- Codex plugin `2026.9.1` installation through `openclaw plugins install @openclaw/codex@2026.9.1 --pin`.
- Explicit Codex plugin activation with native session catalog and supervision enabled before the Gateway starts.
- Codex settings are written only during plugin installation or update, not every time the dashboard opens.
- Embedded rounded NeonX header logo and Windows executable icon.

## Build on Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `dist\windows\NeonX-Agent-Hub.exe`.

Visual Studio and the .NET SDK are not required. The build uses the .NET Framework C# compiler included with Windows.

## Component versions

All platform builds use `config\versions.env` as the single source of truth for pinned component versions:

```env
NEONX_OPENCLAW_VERSION=2026.9.1
NEONX_CODEX_PLUGIN_VERSION=2026.9.1
NEONX_NODEJS_VERSION=26.8.1
NEONX_NODEJS_X64_SHA256=8E1935459A4865CB601930B93D9B47F6B67EB665557CB2593629639DB0DA58AB
NEONX_NODEJS_ARM64_SHA256=B85CE43B6E6EF6B109F1D87F2AFB32B0FB88053AAFCC9E0458CE5E6E403E5B15
```

The Windows build validates this file, exports the values as process environment variables, and generates `platforms\windows\obj\ComponentVersions.cs`. The compiled application also exposes the same variables to child installer processes. Update this file and the matching official Node.js MSI checksums when releasing new component versions.

## Optional code signing

```powershell
.\build.ps1 -CertificatePath C:\certs\neonx.pfx -CertificatePassword "your-password"
```

Code signing is recommended before distribution to reduce SmartScreen warnings. Never store certificates or passwords in the repository.

## Installation log

`%TEMP%\NeonX\OpenClaw-2026.9.1\NeonX-OpenClaw-install.log`

Codex-plugin-only installation log:

`%TEMP%\NeonX\CodexPlugin-2026.9.1\NeonX-CodexPlugin-install.log`
