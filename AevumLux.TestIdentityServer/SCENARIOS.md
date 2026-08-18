# Scenarios

Each scenario is a JSON file under `Scenarios/` that configures a client (secret, scopes, redirect URIs, and — for `cc-expired-tokens` only — an access token lifetime override). **Every scenario's client is loaded and registered together when the test IdentityServer starts** — there's no environment variable or launch profile to pick, and no need to restart the server to switch between them. See `SETUP.md` for how to run the server (there's only one way now).

Kept intentionally small — common real-world mistakes, not an exhaustive matrix.

AevumLux seeds a matching set of scenario providers automatically on first launch (see `ScenarioProviderSeeds` in `AevumLux.Core`) — they show up in Flow Simulator's provider picker only, not in Provider Manager's regular list. Pick the matching one from the dropdown instead of typing values by hand; the tables below are there for reference and for the client secret (not stored on the seeded provider — see each scenario's table). Switching between scenarios is just picking a different one from that dropdown — the server underneath never needs to change.

**Any happy-path scenario can also be used to test a failure, on purpose.** All the fields in Flow Simulator are freely editable after autofilling from a seeded provider — nothing stops you from deliberately typing in a wrong client secret, a redirect URI that doesn't match what's registered, a bad scope, or garbage credentials, even against a "happy path" scenario. You don't need a dedicated `*-wrong-*` scenario to see how the server reacts to a mistake; the dedicated wrong-* scenarios below exist because they demonstrate specific, common real-world misconfigurations worth having a reliable, reproducible client for — not because they're the only way to see a failure.

---

# Client Credentials

Machine-to-machine authentication — no user, no browser. A backend service or job authenticates as *itself* (using its own client ID and secret) to call an API, rather than acting on behalf of a person. Typical real-world use: a nightly batch job, a server-to-server integration, or a CI pipeline calling a protected API.

## cc-happy-path

**What it demonstrates:** Everything correctly configured. A Client Credentials request succeeds and returns a valid, correctly-scoped access token.

**Client to use in AevumLux:**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `cc-happy-path` |
| Client Secret | `happy-path-secret` |
| Scope | `api` |

**Expected result:** `200 OK`, a valid access token, `expires_in` around 900 seconds (15 minutes).

---

## cc-wrong-secret

**What it demonstrates:** The server has the client registered with a *different* secret than the one you're about to send. This is one of the most common real-world integration mistakes — a typo, a rotated secret that wasn't updated everywhere, or copy-pasting the wrong client's credentials.

**Client to use in AevumLux:**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `cc-wrong-secret` |
| Client Secret | *(use anything other than `server-side-secret-that-does-not-match` — e.g. `wrong-secret-on-purpose`)* |
| Scope | `api` |

**Expected result:** `401 Unauthorized`, `error: invalid_client`, `error_description: "The specified client credentials are invalid."`

---

## cc-expired-tokens

**What it demonstrates:** The server issues access tokens with a 1-second lifetime (the shortest OpenIddict supports). By the time you've copied the token into JWT Decoder or Token Validator, it's already expired — a good way to see the "already expired" and expiry-check-failed paths without waiting around or hand-editing a token's `exp` claim.

**Client to use in AevumLux:**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `cc-expired-tokens` |
| Client Secret | `expired-tokens-secret` |
| Scope | `api` |

**Expected result:** `200 OK` with a valid-looking token whose `exp` claim is only ~1 second in the future — likely already expired by the time the response even reaches AevumLux. Token Validator's expiry check fails immediately, and Claims Inspector's Observations panel flags it as "already expired."

---

---

# Authorization Code + PKCE

The flow used when a real person is present: a user signs in through a browser, consents, and the app receives a short-lived authorization code it exchanges for tokens. PKCE (Proof Key for Code Exchange) adds a app-generated secret (the code_verifier) that only the app that started the flow knows, so a stolen authorization code alone can't be redeemed by someone else. Typical real-world use: any app with a login screen — web app, desktop app, mobile app.

This test IdP has no real user store or consent screen, but it does show a real login page in the browser popup — you have to actually type in the test credentials below and submit, so the "user authenticates" step is a real step you go through, not an instant invisible auto-sign-in.

## ac-happy-path

**What it demonstrates:** Everything correctly configured. The authorize redirect shows a login page, signing in succeeds, the code exchange succeeds, and the response includes both an access token and a refresh token (this client also requests `offline_access`).

**Client to use in AevumLux (seeded automatically as "AC — Happy Path"):**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `ac-happy-path` |
| Client Secret | *(none — public client, uses PKCE instead)* |
| Redirect URI | `http://localhost:7890/callback` |
| Scope | `api offline_access` |

**Login page credentials (shown on the page itself, listed here for reference):**
| Field | Value |
|---|---|
| Username | `test-user` |
| Password | `test-password` |

**Expected result:** The Flow Simulator's browser popup shows a login form; enter the credentials above and submit. `200 OK` on the token exchange with a valid access token and refresh token, `expires_in` around 900 seconds. The refresh token can be fed straight into the Refresh Token flow below.

---

## ac-wrong-redirect

**What it demonstrates:** The server has this client registered with a different redirect URI (`http://localhost:9999/callback`) than the one AevumLux will send (`http://localhost:7890/callback`). This is one of the most common Authorization Code setup mistakes — a redirect URI that doesn't exactly match what's registered.

**Client to use in AevumLux (seeded automatically as "AC — Wrong Redirect"):**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `ac-wrong-redirect` |
| Client Secret | *(none — public client)* |
| Redirect URI | `http://localhost:7890/callback` |
| Scope | `api offline_access` |

**Expected result:** The authorize request itself fails — OpenIddict rejects the redirect_uri before the popup ever shows the login page, since it doesn't match what's registered on the server.

---

# Refresh Token

Exchanging a long-lived refresh token for a new access token (and usually a new refresh token) without involving the user or the browser again — this is how a session outlives the access token's short expiry. There's no dedicated scenario config for this: run it directly from Flow Simulator after a successful `ac-happy-path` run, using the refresh_token from that response.

**Refresh tokens rotate and are single-use — but with a 30-second grace window.** By default, OpenIddict (`RefreshTokenReuseLeeway`) allows the exact same original refresh token to be redeemed again within 30 seconds of the first use, before it's treated as truly revoked. This exists to tolerate real-world race conditions — e.g. a client whose response got lost on a flaky connection retrying the same request. If you test reuse by clicking Run repeatedly within a few seconds, every attempt will keep succeeding and returning a new access + refresh token pair — this is correct, expected behavior, not a bug. To actually see reuse get rejected, wait more than 30 seconds between attempts with the *same* original refresh token; you should then get `400 invalid_grant`: `"The specified refresh token has already been redeemed."` (Verified directly against this server via curl on 2026-08-18.)

---

# Device Code

Signing in on a device that has no easy way to type a password — a CLI, a smart TV, a set-top box. The device asks the server for a `device_code` and a short `user_code`, shows the `user_code` and a URL, and polls the token endpoint in the background while the person enters that code on a *different* device (their phone, a nearby computer). Typical real-world use: `gh auth login`, streaming-service TV apps, smart home device setup.

This test IdP has no real user store, but Flow Simulator opens a real popup on the verification URL — you have to type in the test credentials below and submit, same as Authorization Code's login step, before the poll loop can succeed.

## dc-happy-path

**What it demonstrates:** Everything correctly configured. The device authorization request succeeds, a popup opens on the verification page for you to sign in, and once you submit the login form, polling picks up the approval and returns a valid access token.

**Client to use in AevumLux (seeded automatically as "DC — Happy Path"):**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `dc-happy-path` |
| Client Secret | *(none — public client)* |
| Scope | `api` |

**Login page credentials (shown on the page itself, listed here for reference):**
| Field | Value |
|---|---|
| Username | `test-user` |
| Password | `test-password` |

**Expected result:** Step 1 returns a `device_code`/`user_code` pair; step 2 opens a popup on the verification URL — sign in with the credentials above; step 3 polls and succeeds within a few attempts once you've signed in, returning `200 OK` with a valid access token.

---

# Implicit — ⚠ Deprecated

The authorize redirect returns the access token directly in the URL fragment instead of a code to exchange. **Removed in OAuth 2.1.** Included in Flow Simulator specifically to show, with a real request and a real token, where the anti-pattern shows up: the token travels as part of a URL (visible in browser history, `Referer` headers, and URL logging) rather than in a server-to-server POST body, and there's no client authentication step at all. Authorization Code + PKCE replaces this without giving up anything Implicit could do — use it instead.

## implicit-happy-path

**What it demonstrates:** The authorize redirect succeeds and the access token comes back in the URL fragment (`#access_token=...`), with no code-exchange step.

**Client to use in AevumLux (seeded automatically as "Implicit — Happy Path (Deprecated)"):**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `implicit-happy-path` |
| Client Secret | *(none — public client)* |
| Redirect URI | `http://localhost:7890/callback` |
| Scope | `api` |

**Login page credentials (shown on the page itself, listed here for reference):**
| Field | Value |
|---|---|
| Username | `test-user` |
| Password | `test-password` |

**Expected result:** The popup shows a login page — sign in with the credentials above; the popup then redirects with a real access token in the fragment. Flow Simulator shows a deprecation warning alongside the (successful) step explaining exactly why this is discouraged.

---

# ROPC (Resource Owner Password Credentials) — ⚠ Deprecated

The client app collects the user's raw username and password directly (in its own UI) and posts them to the token endpoint — the user never interacts with the identity provider at all. **Dropped in OAuth 2.1.** This defeats the point of delegated authorization: it's incompatible with MFA and federated/SSO login, and trains users to type their password into whatever app asks for it. Authorization Code + PKCE is the replacement — the user authenticates on the identity provider's own page, which the client app never sees the contents of.

## ropc-happy-path

**What it demonstrates:** Everything correctly configured. The token request with a valid username/password succeeds and returns a valid access token — and the request body itself, visible in Flow Simulator, contains the raw password.

**Client to use in AevumLux (seeded automatically as "ROPC — Happy Path (Deprecated)"):**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `ropc-happy-path` |
| Client Secret | `ropc-happy-path-secret` |
| Scope | `api` |
| Username | `test-user` |
| Password | `test-password` |

**Expected result:** `200 OK` with a valid access token. Try the wrong password to see `invalid_grant`. Either way, Flow Simulator shows a deprecation warning alongside the step explaining exactly why this is discouraged.

---

## Adding a new scenario

Name scenarios `{flow-prefix}-{description}` — e.g. `cc-` for Client Credentials, `ac-` for Authorization Code + PKCE, `dc-` for Device Code — so they stay identifiable once there are scenarios for multiple flows.

1. Add `Scenarios/{name}.json` with `name`, `description`, and a `clients` array (see `ScenarioOptions.cs`). Every client_id across every scenario file must be unique — they're all registered together on the one always-running server. If this scenario needs a non-default access token lifetime (like `cc-expired-tokens`' 1 second — the shortest OpenIddict supports, since it truncates to whole seconds), set `accessTokenLifetime` on that client entry specifically — it won't affect any other client.
2. That's it on the server side — the file is picked up automatically the next time the server starts. No launch profile, no `.bat` file, no environment variable to add.
3. Add a matching seeded provider in `AevumLux.Core`'s `ScenarioProviderSeeds` so it shows up in Flow Simulator's picker.
4. Document it here, under that flow's section. If this is the first scenario for a new flow, add a `# Flow Name` header above it with a one/two-sentence explanation of what the flow is used for and why (see the "Client Credentials" section above for the pattern) — this same blurb is intended to eventually show up in Flow Simulator's own UI when that flow is selected, so write it for that audience too, not just this doc.
