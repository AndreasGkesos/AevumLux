# AevumLux
### *an age of light*

> **because your auth flows deserve to be seen**

AevumLux is a native Windows desktop developer tool for inspecting, debugging and understanding OpenID Connect and OAuth 2.0 flows. Built for developers who are tired of guessing what their tokens actually contain, why their flows are failing, and what their provider's discovery document is actually telling them.

---

## What it is

A portfolio-grade WinUI 3 desktop application that gives you:

- A structured, readable view of any provider's OIDC discovery document
- JWT decoding with header, payload and signature broken down into readable fields
- Cryptographic token validation against a provider's JWKS, with a pass/fail reason per check
- Claims inspection grouped by identity, access and metadata, with security observations flagged automatically
- JWKS Explorer that makes cryptographic keys tangible, with kid matching against a token
- Scope analysis — standard vs custom scopes, plain English descriptions, cross-checked against what the token actually contains
- Token diff — paste two tokens and see exactly what changed
- Provider Manager for saving frequently-used issuer/JWKS URLs
- (Planned) Step-by-step, live flow simulation for Authorization Code + PKCE, Client Credentials, Device Code, Refresh Token and Implicit (deprecated — clearly labelled) — a study-mode feature, off by default

---

## Features

Navigation is organised into tiers, roughly by how central each page is to real debugging work: core debugging tools first, informational/comparison tools second, and Flow Simulator — a study-mode feature — hidden behind a Settings toggle, off by default.

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
| Flow Simulator | Step-by-step simulation of OAuth 2.0 / OIDC flows (Authorization Code + PKCE, Client Credentials, Device Code, Refresh Token, Implicit) with full, live HTTP request/response visibility against a real provider | Not built — hidden by default behind a Settings toggle; waiting on a self-hosted test IdentityServer |
| Settings | Currently just the Flow Simulator visibility toggle | Minimal |

Two items from earlier planning are worth calling out explicitly rather than leaving unmentioned:
- **Token Expiry Monitor** (live countdown timers) was built and then removed — JWT Decoder's and Token Validator's existing expiry handling already cover real debugging needs.
- **Export** (copy tokens as JSON, export discovery docs, flow reports as Markdown/HTML) has not been discussed or scheduled yet.

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
| Secret Encryption | Windows Data Protection API (DPAPI) |
| Browser Redirect | WebView2 |
| Design | Fluent UI, Mica background, Windows 11 design language |

---

## Architecture

Two projects in one solution:

- **AevumLux** — WinUI 3 app. Views (XAML only, no logic), ViewModels, DI bootstrapping in `App.xaml.cs`.
- **AevumLux.Core** — Class library with zero UI dependency. All business logic, services, models, repositories and security utilities live here.

Key patterns:
- Strict MVVM — no logic in code-behind files
- All services injected through interfaces via `Microsoft.Extensions.DependencyInjection`
- Repository pattern over LiteDB — no raw database calls outside repositories
- DPAPI available (`ICryptoService`/`DpapiCryptoService`) for client secrets before they touch the database — not yet exercised, since Provider Manager doesn't store client secrets yet
- Nullable reference types enabled, guard clauses on all public methods
- Feature-based folder structure
- App data (`aevumlux.db`, `settings.json`) lives at `%LOCALAPPDATA%\AevumLux\` — the app is unpackaged (no MSIX identity), so WinUI's `ApplicationData` API is not used for persistence anywhere in this project

```
AevumLux/
├── AevumLux/                   # WinUI 3 app
│   ├── Views/                  # XAML pages — one folder per feature
│   ├── ViewModels/             # One ViewModel per page
│   └── App.xaml.cs             # DI container registration
│
└── AevumLux.Core/              # Class library — no UI dependency
    ├── Models/
    ├── Services/Interfaces/
    ├── Services/Implementations/
    ├── Repositories/Interfaces/
    ├── Repositories/Implementations/
    ├── Security/               # ICryptoService, DpapiCryptoService
    └── Helpers/                # Guard
```

---

## Getting Started

### Prerequisites

- Windows 10 version 1809 or later (Windows 11 recommended)
- Visual Studio 2022 17.8+ with the **Windows application development** workload
- .NET 8 SDK
- Windows App SDK 1.6

### Build

```
git clone https://github.com/yourusername/AevumLux.git
cd AevumLux
# Open AevumLux.sln in Visual Studio 2022
# Set AevumLux as startup project
# Build and run (x64 recommended)
```

---

## Screenshots

*Coming soon.*

---

## Contributing

This is a portfolio project. Issues and pull requests are welcome.

---

## License

MIT
