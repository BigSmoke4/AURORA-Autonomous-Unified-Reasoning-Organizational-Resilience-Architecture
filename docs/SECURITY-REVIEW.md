# Security Review

This review checks the running application against the controls the README claims. It was done by reading the code,
checking each control in the source, and running the automated checks in CI. It is not an external penetration test,
and it is not a substitute for one.

## Scope and method
- Authentication and session: cookies, lockout, password policy, sign-out.
- Authorization: controllers, permission policies, tenant scoping, SignalR hubs.
- Browser protections: response headers, Content Security Policy, anti-forgery on writes.
- Input and resource limits: forms, uploads, feed payloads, rate limits.
- Secrets and transport: API keys for feeds and the model, HTTPS enforcement, container user.
- Automated analysis: CodeQL (`.github/workflows/codeql.yml`, run on every push and weekly), the vulnerable-package
  audit, and the container smoke check in CI.

## Results

| # | Area | What was checked | Result |
|---|------|------------------|--------|
| 1 | Cookies | `HttpOnly`, `SameSite=Strict`, `Secure` (always outside Development), 8-hour sliding expiry, name `aurora.auth` | Verified in `Program.cs`. |
| 2 | Lockout | 5 failed attempts lock the account for 15 minutes (ASP.NET Identity, per user). Sign-in is also limited to 10 attempts per minute per client address. | Verified. |
| 3 | Password policy | At least 12 characters, with upper case, a digit, a non-alphanumeric character, and 4 unique characters. | Verified. |
| 4 | Response headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, and a Content Security Policy with `script-src 'self'` and `style-src 'self'`. HSTS when `Security:RequireHttps` is on. | Verified. The container smoke check asserts `nosniff` and the CSP. |
| 5 | Anti-forgery | All 32 `[HttpPost]` actions carry `[ValidateAntiForgeryToken]`. Sign-out is a POST, so a cross-site link cannot sign a user out. | Verified by counting the attributes in the source. |
| 6 | Authorization | Module controllers require a permission policy. `HomeController.Error`, sign-in, and access-denied are anonymous by design. `ModuleControllerBase` is abstract. | Verified. Tenant scope comes from the signed-in principal. |
| 7 | SignalR | Both hubs have `[Authorize]`. `OrganizationHub` joins the caller to its own organization's group. `SimulationHub` names groups from the caller's organization claim, so a user cannot join another tenant's group. | Verified. |
| 8 | Cross-site WebSocket | SignalR does not check the `Origin` header. The `SameSite=Strict` cookie is not sent on cross-site requests, so a foreign page cannot authenticate a socket. | Mitigated by cookie policy. No explicit origin check exists. See residual risks. |
| 9 | Uploads | There are no file uploads. Knowledge documents are text forms with a 100,000-character body limit and a 200-character title limit. Kestrel's default body limit is in force. | Verified. |
| 10 | Feed payloads | Live payloads are capped at 5 MB while they are read. Invalid rows are rejected one at a time. | Fixed in this release (the cap is new). |
| 11 | Feed keys | Feed keys are sent as bearer tokens only over https. Error messages do not include the key or the response body. Startup rejects an unencrypted feed endpoint. | Fixed in this release (startup validation is new). Covered by tests. |
| 12 | Model key | The model endpoint was accepted over plain http, so its API key could be sent in the clear. | **Finding, fixed.** `AiOptionsValidator` now requires https, or http on a loopback host, and validates on startup. |
| 13 | Client address | The sign-in limit used the connection's remote address. Behind a reverse proxy, every client would share the proxy's address. | **Finding, fixed.** Forwarded headers are trusted only from loopback and from `Security:TrustedProxies`. |
| 14 | Cache | Narratives are cached under a hash of the model version, the prompt, and the evidence. Cache failures never fail a request. | Verified. See residual risks for cross-tenant reuse. |
| 15 | Container | The runtime image runs as the non-root `app` user. | Verified in the `Dockerfile`. |
| 16 | Errors | Developer exception pages only in Development. Production uses the exception handler. | Verified in `Program.cs`. |
| 17 | Dependencies | The vulnerable-package audit runs in CI before the build. | Verified in `ci.yml`. |
| 18 | Logging of secrets | No logger call or error message writes an API key. | Verified by searching the source for key references in log and error paths. |
| 19 | Load | 20 requests per second for two minutes against the production container. Failure rate, latency, and check thresholds fail the job. | Added (`tests/load/aurora-smoke.js`). This is a smoke load test, not a soak. |

## Findings and what changed
- **Model key over plain http (12).** A deployment could have sent the model key without encryption. Startup now refuses
  such a configuration, unless the host is loopback, where the key does not leave the machine. Tests cover both cases.
- **Shared client address behind a proxy (13).** The login limit could have locked out every user of a deployment at
  once, or let one client avoid it by changing headers. Only configured proxies may now supply the address.
- **Feed endpoint validation (11) and payload cap (10).** Added with the live feed adapters.

## Residual risks, accepted
- **No external penetration test.** This review is by reading code and running CI. A third-party test has not been done.
- **No dynamic scan.** No DAST tool (such as OWASP ZAP) runs against the running application.
- **Cross-site WebSocket (8).** Protection depends on the cookie policy, not an explicit `Origin` check. A browser that
  ignores `SameSite` would weaken it. Low severity, because the hub also requires authentication.
- **Cache key shares entries across tenants.** Two tenants with byte-identical evidence share one cached narrative.
  The narrative is derived only from that evidence, so no other tenant's data is returned. A tenant holding the exact
  evidence could test whether another tenant produced the same narrative. Low severity.
- **Sliding session of 8 hours.** A stolen session stays valid while it is used. Shorten `ExpireTimeSpan` if the risk
  appetite requires it.
- **Client address trust depends on configuration.** If the reverse proxy is not listed in `Security:TrustedProxies`,
  the limit applies to the proxy's address. This is a deployment requirement, documented in the README.
- **Live feeds and model endpoints were not exercised.** Their fixtures and stubs are tested. A live provider's
  behaviour, including its error responses, is unverified until its opt-in test runs with real credentials.
