# AevumLux
### *an age of light*

> **because your auth flows deserve to be seen**

AevumLux is a native Windows desktop developer tool for inspecting, debugging and understanding OpenID Connect and OAuth 2.0 flows. Built for developers who are tired of guessing what their tokens actually contain, why their flows are failing, and what their provider's discovery document is actually telling them.

---

## What it is

A portfolio-grade WinUI 3 desktop application that gives you:

- A structured, readable view of any provider's OIDC discovery document
- JWT decoding with color-coded header, payload and signature sections
- Cryptographic token validation against a provider's JWKS
- Step-by-step flow simulation for Authorization Code + PKCE, Client Credentials, Device Code, Refresh Token and Implicit (deprecated — clearly labelled)
- Context-aware error explanation at every flow step
- Claims inspection grouped by identity, access and metadata
- JWKS Explorer that makes cryptographic keys tangible
- Scope analysis with plain English descriptions for all standard OIDC scopes
- Token diff — paste two tokens and see exactly what changed
- Live expiry countdown timers for access, ID and refresh tokens
- Provider Manager with built-in presets for Keycloak, Azure AD, Auth0 and Okta
- In-session history of every token decoded and every flow run

---

## Features

| Feature | Description |
|---|---|
| Discovery Explorer | Fetch and browse `.well-known/openid-configuration` in a structured layout |
| JWT Decoder | Decode any JWT — header, payload, signature — with human-readable field names and expiry countdown |
| Token Validator | Validate signature, expiry, issuer and audience against a JWKS with per-check results |
| Flow Simulator | Step-by-step simulation of six OAuth 2.0 / OIDC flows with full HTTP request/response visibility |
| Error Explainer | Context-aware error explanation — the same error code explained differently at each step |
| Claims Inspector | Structured claims table grouped by category with plain English descriptions |
| JWKS Explorer | Visual display of all keys with kid matching against a provided token |
| Scope Analyser | Plain English description of every scope in a token |
| Token Diff | Side-by-side diff with green/red highlights for added, removed and changed claims |
| Expiry Monitor | Live countdown timers with visual expiry alerts |
| Provider Manager | Save multiple providers and environments with DPAPI-encrypted secrets |
| Session History | In-session log of all activity — cleared on close unless explicitly saved |
| Export | Copy tokens as JSON, export discovery docs, save flow reports as Markdown or HTML |
| Settings | Theme, data management, DB location, version info |

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
- DPAPI encryption on all client secrets before they touch the database
- Nullable reference types enabled, guard clauses on all public methods
- Feature-based folder structure

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
    └── Helpers/                # PkceHelper, JwtHelper, Guard
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
