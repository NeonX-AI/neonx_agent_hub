# NeonX Agent Hub for macOS

This directory is reserved for the future macOS implementation.

The macOS app should reuse the root `assets` branding and match the Windows agent catalog behavior:

- Detect installed agents.
- Offer Install or Open actions.
- Use the system Node.js/npm installation without downloading a private runtime.
- Open each agent's native dashboard or control interface.

Pinned component versions are shared through `config/versions.env`. A future macOS build must load this file rather than duplicating Node.js or OpenClaw versions in platform source code.
