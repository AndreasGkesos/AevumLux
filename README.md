# AevumLux

A Windows desktop app I built to actually understand OpenID Connect and OAuth 2.0 — not just read about them. It's a learning/portfolio project, not a finished product: the parts that inspect tokens and discovery documents (JWT Decoder, Token Validator, Claims Inspector, JWKS Explorer, Scope Analyser, Token Diff) work against any standards-compliant provider you point them at, but the flow simulation part was built and tested against a self-hosted test identity server included in this repo, not against real-world providers like Auth0, Okta, or Azure AD. If you try Flow Simulator against a production IdP, expect friction — different providers implement these flows with real variations, and this project hasn't been exercised against them.

---

## Features

Navigation is organised into tiers, roughly by how central each page is to real debugging work: core debugging tools first, informational/comparison tools second. Flow Simulator is always available; a Settings toggle controls whether it shows scenario picker/teaching text (and whether Flow Explanations appears in navigation at all) or stays a clean, minimal debugging tool.

| Feature | Description | Status |
|---|---|---|
| Discovery Explorer | Fetch and browse `.well-known/openid-configuration` in a structured layout | Built |
| JWT Decoder | Decode any JWT — header, payload, signature — with human-readable field names and expiry status | Built |
| Token Validator | Validate signature, expiry, issuer and audience against a JWKS with per-check results | Built |
| Session History | In-session log of all activity, cleared on close | Backend logs everything already; page UI not built yet |
| Claims Inspector | Claims grouped by category with plain English descriptions, plus security observations (weak/missing algorithms, missing claims, long-lived tokens) | Built |
| JWKS Explorer | Visual display of all keys with kid matching against a provided token | Built |
| Scope Analyser | Standard vs custom scope classification, plain English descriptions, and cross-check against claims actually present in the token | Built |
| Token Diff | Side-by-side diff with green/red/amber highlights for added, removed and changed claims | Built |
| Provider Manager | Save named providers (name, issuer URL, JWKS URI) for quick reuse | Built (thin — no client secrets/flow config yet, not yet wired into other pages) |
| Flow Simulator | Step-by-step simulation of OAuth 2.0 / OIDC flows (Authorization Code + PKCE, Client Credentials, Device Code, Refresh Token, Implicit, ROPC) with full, live HTTP request/response visibility, shown as a two-column client/IdP timeline | Built and verified against the bundled AevumLux.TestIdentityServer; not tested against real-world providers |
| Flow Explanations | Static reference — one page per flow, what it is, real-world usage, request/response contract | Built |
| Settings | Toggle for showing/hiding Flow Explanations and per-step teaching text in Flow Simulator; when on, also shows the Test Identity Provider's status, Start/Stop controls, local URL and live log | Minimal |

---

## Known limitations

- **The installer's optional install-time choices (Desktop shortcut, and an uninstall-time "also delete my data" option) don't have UI checkboxes yet.** Both exist as MSI properties (`INSTALLDESKTOPSHORTCUT`, `DELETE_USER_DATA`) with sensible defaults — Desktop shortcut on by default, user data preserved by default — but can currently only be overridden via `msiexec` command-line flags, not through the installer's own dialogs.
- **A device_code verification edge case in the test server can throw instead of failing cleanly.** Submitting an invalid/expired `user_code` to `AevumLux.TestIdentityServer`'s `/connect/verify` endpoint can hit an `ArgumentNullException` inside OpenIddict's own device-flow internals rather than returning a normal failed result. It's caught and handled as a clean failure, but the underlying cause hasn't been tracked down — see the `TODO` comment in `Program.cs`.
- **No "decode this token" shortcut between pages.** Flow Simulator can't jump a token it just received straight into JWT Decoder or Claims Inspector — every page in the app is currently parameterless and resolved fresh, so there's no cross-page navigation-with-data mechanism yet. Copy/paste works fine in the meantime.
- **Session History has no UI.** The backend already logs every Decode/Fetch/Validate action in-session, but there's no page to view that history yet.
- **Provider Manager isn't wired into the other pages.** Saved providers can be created and managed, but Discovery Explorer, Token Validator and JWKS Explorer don't yet offer a "pick a saved provider" dropdown — only Flow Simulator reads from the provider list right now.

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | WinUI 3 with Windows App SDK |
| Language | C# on .NET 8 |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| OIDC Client | IdentityModel.OidcClient |
| JWT | System.IdentityModel.Tokens.Jwt |
| HTTP | Microsoft.Extensions.Http |
| Local Storage | LiteDB |
| Browser Redirect | WebView2 |
| Design | Fluent UI, Mica background, Windows 11 design language |

---

## Architecture

Three projects in one solution:

- **AevumLux** — the WinUI 3 desktop app. Strict MVVM: Views are XAML only, all logic lives in ViewModels.
- **AevumLux.Core** — class library with zero UI dependency. All business logic, services, models and repositories live here, injected through interfaces via `Microsoft.Extensions.DependencyInjection`.
- **AevumLux.TestIdentityServer** — a small, self-hosted OpenIddict-based OAuth/OIDC server used to drive Flow Simulator's live testing. Runs standalone; see its own `SETUP.md`/`SCENARIOS.md`.
- **AevumLux.Installer** — a WiX Toolset project that packages AevumLux and the published TestIdentityServer into one self-contained MSI installer. Lives outside the `.sln` (a different toolchain, not something you build/debug day to day) — see "Building the installer" below.

App data (`aevumlux.db`, `settings.json`) lives at `%LOCALAPPDATA%\AevumLux\` — the app is unpackaged, so no MSIX/`ApplicationData` persistence is used.

---

## Getting Started

### Prerequisites

- Windows 10 version 1809 or later (Windows 11 recommended)
- Visual Studio 2022 17.8+ with the **Windows application development** workload
- .NET 8 SDK
- Windows App SDK 1.6

### Build and run

```
git clone https://github.com/AndreasGkesos/AevumLux.git
cd AevumLux
# Open AevumLux.sln in Visual Studio 2022
# Set AevumLux as the startup project
# Build and run (x64 recommended)
```

Discovery Explorer, JWT Decoder, Token Validator, Claims Inspector, JWKS Explorer, Scope Analyser and Token Diff all work immediately against any real OIDC provider's issuer URL.

### Trying Flow Simulator

Flow Simulator needs a running OAuth/OIDC server to actually call. This repo includes one for exactly that — `AevumLux.TestIdentityServer`, a small self-hosted OpenIddict-based server with a handful of pre-seeded test scenarios (happy paths and common misconfigurations). The main app manages it for you:

- On first run, AevumLux automatically publishes `AevumLux.TestIdentityServer` as a self-contained executable into a `TestIdp\` folder next to its own `.exe` — no manual `dotnet run`, no second terminal.
- Go to **Settings** and turn on **Show flow explanations** to reveal the **Test Identity Provider** section — a status indicator, Start/Stop buttons, the local URL it's running on, and a live console log.
- If you try to run a Flow Simulator scenario against the local test IdP while it's stopped, you'll get an inline prompt to start it instead of a raw connection error.
- If you change `AevumLux.TestIdentityServer`'s code, run `republish-testidp.bat` in the repo root to overwrite the published copy the app is using — the app only auto-publishes when that folder is empty, so it won't pick up code changes on its own after the first run.

`AevumLux.TestIdentityServer` itself remains fully independent — it can still be built and run on its own from Visual Studio or `dotnet run`, with no dependency on the main app. See `AevumLux.TestIdentityServer/SETUP.md` for setup details and `SCENARIOS.md` for what each seeded scenario demonstrates.

### Building the installer

For a standalone install with no .NET SDK or Visual Studio required on the target machine, run:

```
build-installer.bat
```

from the repo root. This publishes both `AevumLux` and `AevumLux.TestIdentityServer` as self-contained win-x64 executables, then packages them into `AevumLux.Installer\bin\Release\AevumLux.Installer.msi`. The resulting MSI is fully self-contained (embeds its payload) and can be copied/run anywhere.

The installer places `AevumLux.exe` and a bundled, pre-published `TestIdp\` folder side by side in `Program Files\AevumLux\` — the same relative layout as a dev build, so the running app finds the test IdP identically either way. It also copies `ReadMe.txt` and `Scenarios.md` alongside the installed exe, since an installed-only user has no repo to read those from.

Run it again after changing app or test-server code to cut a new installer build. Bump `<Version>` in `AevumLux/AevumLux.csproj` and `Product.wxs`'s `Package/Version` together before a real release — installing a newer version over an older one on the same machine upgrades in place (same install folder, user data untouched); running an older MSI over a newer install is blocked with a clear message instead of silently downgrading.

---

## License

MIT
