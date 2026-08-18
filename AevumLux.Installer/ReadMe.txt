AevumLux
========

A Windows desktop tool for understanding and debugging OAuth 2.0 / OpenID
Connect. It's a learning/portfolio project, not a finished product.

The token-inspection tools — JWT Decoder, Token Validator, Claims Inspector,
JWKS Explorer, Scope Analyser, Token Diff — work against any real
standards-compliant provider you point them at. Flow Simulator was built and
tested against the bundled test identity server (installed alongside this
app, in the TestIdp\ folder), not against real-world providers like Auth0,
Okta, or Azure AD — expect friction if you try it there.


Trying Flow Simulator
----------------------

Flow Simulator needs a running OAuth/OIDC server to actually call. This
install includes one for exactly that purpose.

- Open the app and go to Settings, then turn on "Show flow explanations" —
  this reveals a Test Identity Provider section with a status indicator,
  Start/Stop buttons, the local URL it's running on, and a live console log.
- Click Start, then go to Flow Simulator, pick one of the seeded scenario
  providers from the dropdown, and press Run.
- If you try to run a scenario while the test IdP is stopped, Flow Simulator
  shows an inline prompt to start it, instead of a raw connection error.

See Scenarios.md (included alongside this file — opens fine in Notepad) for
the client secrets and test credentials each seeded scenario expects, and
what result to expect from each one.


What each flow demonstrates
----------------------------

- Client Credentials — machine-to-machine auth, no user involved.
- Authorization Code + PKCE — the flow used when a real person signs in
  through a browser. The current recommended flow for apps with a login
  screen.
- Refresh Token — exchanging a long-lived refresh token for a new access
  token without involving the user again.
- Device Code — signing in on a device with no easy way to type a password
  (a CLI, a smart TV) by entering a short code on a second device.
- Implicit (deprecated) — included to show, with a real token, exactly why
  this flow was removed in OAuth 2.1.
- ROPC (deprecated) — included to show why collecting a user's raw password
  directly in a client app defeats the point of delegated authorization.


Your data
----------

Settings and local data live in:
  %LOCALAPPDATA%\AevumLux\

Uninstalling AevumLux leaves this folder alone by default, so your settings
survive a reinstall or upgrade.


License
--------

MIT License. See License.rtf, included with this installer.
