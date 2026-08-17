# Scenarios

Each scenario is a JSON file under `Scenarios/` that configures the test IdentityServer's client(s), scopes and access token lifetime. See `SETUP.md` for how to run one.

Kept intentionally small — common real-world mistakes, not an exhaustive matrix.

A note on AevumLux's own Provider Manager: it does not yet have a way to seed a matching set of "scenario providers" automatically (planned — see project notes). Until then, add each client below manually via AevumLux's Flow Simulator/Provider Manager form using the values shown.

---

# Client Credentials

Machine-to-machine authentication — no user, no browser. A backend service or job authenticates as *itself* (using its own client ID and secret) to call an API, rather than acting on behalf of a person. Typical real-world use: a nightly batch job, a server-to-server integration, or a CI pipeline calling a protected API.

## cc-happy-path

**What it demonstrates:** Everything correctly configured. A Client Credentials request succeeds and returns a valid, correctly-scoped access token.

**Run:** `run-cc-happy-path.bat`, or the `cc-happy-path` launch profile in Visual Studio.

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

**Run:** `run-cc-wrong-secret.bat`, or the `cc-wrong-secret` launch profile in Visual Studio.

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

**What it demonstrates:** The server issues access tokens with a 5-second lifetime. By the time you've copied the token into JWT Decoder or Token Validator, it's already expired — a good way to see the "already expired" and expiry-check-failed paths without waiting around or hand-editing a token's `exp` claim.

**Run:** `run-cc-expired-tokens.bat`, or the `cc-expired-tokens` launch profile in Visual Studio.

**Client to use in AevumLux:**
| Field | Value |
|---|---|
| Issuer URL | `http://localhost:7087` |
| Client ID | `cc-expired-tokens` |
| Client Secret | `expired-tokens-secret` |
| Scope | `api` |

**Expected result:** `200 OK` with a valid-looking token whose `exp` claim is only ~5 seconds in the future. Paste it into JWT Decoder within a few seconds and it shows as valid; wait past 5 seconds and Token Validator's expiry check fails, and Claims Inspector's Observations panel flags it as "already expired."

---

## Adding a new scenario

Name scenarios `{flow-prefix}-{description}` — e.g. `cc-` for Client Credentials, `ac-` for Authorization Code + PKCE, `dc-` for Device Code — so they stay identifiable once there are scenarios for multiple flows.

1. Add `Scenarios/{name}.json` with `name`, `description`, `accessTokenLifetime`, and a `clients` array (see `ScenarioOptions.cs`).
2. Add a matching profile to `Properties/launchSettings.json` setting `ACTIVE_SCENARIO` to the file name.
3. Add a `run-{name}.bat` following the pattern in `SETUP.md`.
4. Document it here, under that flow's section. If this is the first scenario for a new flow, add a `# Flow Name` header above it with a one/two-sentence explanation of what the flow is used for and why (see the "Client Credentials" section above for the pattern) — this same blurb is intended to eventually show up in Flow Simulator's own UI when that flow is selected, so write it for that audience too, not just this doc.
