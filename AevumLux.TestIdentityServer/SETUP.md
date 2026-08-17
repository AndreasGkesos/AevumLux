# AevumLux Test IdentityServer — Setup

A minimal, self-hosted OpenIddict-based OAuth 2.0 / OIDC server for testing and demoing AevumLux's Flow Simulator. State is deliberately **not persisted** — every scenario starts from a clean, known configuration each time the server starts.

It is a normal ASP.NET Core project, so it can be run two different ways depending on what you're doing.

---

## Option 1 — Developing or switching scenarios (Visual Studio)

Open `AevumLux.sln`, set **AevumLux.TestIdentityServer** as the project you're debugging (right-click the project → Debug → Start New Instance — leave AevumLux itself as the solution's main startup project), and pick a scenario from the launch profile dropdown next to the Run button:

- `cc-happy-path`
- `cc-wrong-secret`
- `cc-expired-tokens`

The `cc-` prefix identifies which flow the scenario belongs to (**c**lient **c**redentials) — future scenarios for other flows will use their own prefix (e.g. `ac-` for Authorization Code + PKCE), so scenarios stay identifiable at a glance once there are more of them.

Each profile just sets `ACTIVE_SCENARIO` before launch. To add a new scenario, add a JSON file under `Scenarios/` (see `SCENARIOS.md`) and a matching profile in `Properties/launchSettings.json`.

The server listens on `http://localhost:7087` in every profile, matching what the rest of AevumLux's docs/screenshots assume.

---

## Option 2 — Standalone, no Visual Studio required

Publish a self-contained build once:

```powershell
dotnet publish AevumLux.TestIdentityServer -c Release -r win-x64 --self-contained -o publish
```

This produces a `publish/` folder containing `AevumLux.TestIdentityServer.exe` and everything it needs — no .NET SDK required on the machine running it.

To run a specific scenario without editing anything, set the `ACTIVE_SCENARIO` environment variable before launching. The simplest way is a tiny batch script per scenario, placed next to the exe:

**`run-cc-happy-path.bat`**
```bat
@echo off
set ACTIVE_SCENARIO=cc-happy-path
set ASPNETCORE_URLS=http://localhost:7087
AevumLux.TestIdentityServer.exe
```

**`run-cc-wrong-secret.bat`**
```bat
@echo off
set ACTIVE_SCENARIO=cc-wrong-secret
set ASPNETCORE_URLS=http://localhost:7087
AevumLux.TestIdentityServer.exe
```

**`run-cc-expired-tokens.bat`**
```bat
@echo off
set ACTIVE_SCENARIO=cc-expired-tokens
set ASPNETCORE_URLS=http://localhost:7087
AevumLux.TestIdentityServer.exe
```

Copy these into the `publish/` folder alongside the exe. Double-click the one for the scenario you want; the console window stays open while the server runs. Close it (or Ctrl+C) before starting a different scenario's script — only one scenario can be active per running instance.

If `ACTIVE_SCENARIO` is not set, the server defaults to `cc-happy-path`.

---

## Why HTTP, not HTTPS

This server disables OpenIddict's transport security requirement (`DisableTransportSecurityRequirement()`) so it can be hit over plain `http://localhost` without needing a trusted dev certificate — this keeps the standalone/batch-script workflow simple. **This is a local test tool only; never do this in a real deployment.**

## Why no persistence

Every scenario is meant to represent one clean, reproducible starting state. Using EF Core's in-memory provider means each server start (or scenario switch) begins from nothing — the seeded client(s)/scope(s) from that scenario's JSON file, and nothing else. If the server crashes or you need to reset state, just restart it.

---

## Next: what to actually try

See `SCENARIOS.md` for what each scenario demonstrates and which AevumLux setup to pair it with.
