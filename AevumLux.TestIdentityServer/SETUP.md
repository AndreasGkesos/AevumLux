# AevumLux Test IdentityServer — Setup

A minimal, self-hosted OpenIddict-based OAuth 2.0 / OIDC server for testing and demoing AevumLux's Flow Simulator. State is deliberately **not persisted** — every server start begins from the same clean, known configuration.

**Every scenario's client is loaded and registered at startup, all at once.** There's no environment variable to set and no scenario to pick when launching this server — it's always the same, always-on process. Switching what you're testing is done entirely from AevumLux's own Flow Simulator scenario picker, by choosing a different seeded provider — never by restarting this server.

It is a normal ASP.NET Core project, so it can be run two different ways depending on what you're doing.

---

## Option 1 — Developing (Visual Studio)

Open `AevumLux.sln`, set **AevumLux.TestIdentityServer** as the project you're debugging (right-click the project → Debug → Start New Instance — leave AevumLux itself as the solution's main startup project), and press Run. There's only one launch profile; every scenario's client comes up together.

To add a new scenario, add a JSON file under `Scenarios/` (see `SCENARIOS.md`) — it's picked up automatically on the next server start, no other configuration needed.

The server listens on `http://localhost:7087`, matching what the rest of AevumLux's docs/screenshots assume.

---

## Option 2 — Standalone, no Visual Studio required

Publish a self-contained build once:

```powershell
dotnet publish AevumLux.TestIdentityServer -c Release -r win-x64 --self-contained -o publish
```

This produces a `publish/` folder containing `AevumLux.TestIdentityServer.exe` and everything it needs — no .NET SDK required on the machine running it. Make sure the `Scenarios/` folder is copied alongside the exe.

Run it directly, or use `run.bat` (copy it into the `publish/` folder alongside the exe):

```bat
@echo off
set ASPNETCORE_URLS=http://localhost:7087
AevumLux.TestIdentityServer.exe
```

Leave the console window open while you test — there's no reason to restart it between scenarios.

---

## Why HTTP, not HTTPS

This server disables OpenIddict's transport security requirement (`DisableTransportSecurityRequirement()`) so it can be hit over plain `http://localhost` without needing a trusted dev certificate. **This is a local test tool only; never do this in a real deployment.**

## Why no persistence

Using EF Core's in-memory provider means every server start begins from nothing but the seeded clients/scopes from the `Scenarios/` files — no leftover state from a previous session. If the server crashes or you need to reset everything, just restart it; all scenarios come back exactly as they were.

## Why one client can have a different token lifetime than another

`cc-expired-tokens`' client sets a 5-second access token lifetime via OpenIddict's per-identity `ClaimsIdentity.SetAccessTokenLifetime`, applied only when that specific client_id signs in — every other client on this same running server keeps the normal 15-minute default set at the server level. This is what lets a scenario like "already-expired tokens" coexist with all the happy-path scenarios on one always-on process, instead of needing its own restart with a different server-wide setting.

---

## Next: what to actually try

See `SCENARIOS.md` for what each scenario demonstrates and which AevumLux setup to pair it with.
