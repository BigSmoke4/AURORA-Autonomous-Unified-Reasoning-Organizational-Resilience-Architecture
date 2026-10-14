# Security Review

This review checks the running application against the controls the README claims. It was done by reading the code,
checking each control in the source, running the regression tests, and running the automated scans in CI. It is not an
external penetration test, and it is not a substitute for one.

## Scope and method
- Authentication and session: cookies, lockout, password policy, sign-out, session lifetime.
- Authorization: controllers, permission policies, tenant scoping, SignalR hubs and their origin check.
- Browser protections: response headers, Content Security Policy, cross-origin isolation, anti-forgery on writes.
- Input and resource limits: forms, uploads, feed payloads, knowledge corpus size, rate limits.
- Secrets and transport: API keys for feeds and the model, HTTPS enforcement, container user.
- Automated analysis:
  - CodeQL (`.github/workflows/codeql.yml`), on every push and weekly.
  - The vulnerable-package audit, before every build.
  - The HTTP regression tests in `tests/Aurora.Tests/Security`, on every push.
  - The container smoke check and the k6 smoke load test, on every push (`.github/workflows/ci.yml`).
  - An OWASP ZAP baseline (passive) scan on every push.
  - A nightly job (`.github/workflows/nightly.yml`) with a 30-minute constant-rate k6 soak and an active OWASP ZAP full
    scan. The scan signs in as the bootstrap administrator of the disposable container. Its password is generated for
    each run and never stored, and the scan's session cookies are passed to ZAP.
  - Live feed and model checks (`.github/workflows/live-tests.yml`), which run only when their endpoints are configured.
- Scan findings are published as check annotations, one per ZAP alert, and each one is reviewed by hand.

## Results

| # | Area | What was checked | Result |
|---|------|------------------|--------|
| 1 | Cookies and session | `HttpOnly`, `SameSite=Strict`, `Secure` (always outside Development), name `aurora.auth`. The lifetime is a fixed 8 hours from sign-in, with sliding renewal disabled. | Verified in `Program.cs`. **Finding, fixed.** The session used to renew on every request, so it could last indefinitely. |
| 2 | Lockout | 5 failed attempts lock the account for 15 minutes (ASP.NET Identity, per user). Sign-in is also limited to 10 attempts per minute per client address. | Verified. Covered by `SecurityRegressionHttpTests`. |
| 3 | Password policy | At least 12 characters, with upper case, a digit, a non-alphanumeric character, and 4 unique characters. | Verified. |
| 4 | Response headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, and a Content Security Policy with `script-src 'self'` and `style-src 'self'`. HSTS when `Security:RequireHttps` is on. | Verified. The container smoke check asserts `nosniff` and the CSP. |
| 5 | Anti-forgery | All 32 `[HttpPost]` actions carry `[ValidateAntiForgeryToken]`. A POST without a token is rejected (`APostWithoutAnAntiforgeryToken_IsRejected`). Sign-out is a POST, so a cross-site link cannot sign a user out. | Verified by counting the attributes in the source, and by the HTTP test. |
| 6 | Authorization | Module controllers require a permission policy. `HomeController.Error`, sign-in, and access-denied are anonymous by design. `ModuleControllerBase` is abstract. | Verified. Tenant scope comes from the signed-in principal. |
| 7 | SignalR | Both hubs have `[Authorize]`. `OrganizationHub` joins the caller to its own organization's group. `SimulationHub` names groups from the caller's organization claim. | Verified. `AHubConnection_RequiresASignedInUser` covers the anonymous case. |
| 8 | Cross-site WebSocket | `AuroraHub` checks the `Origin` header. A connection is accepted only when its origin matches the request's scheme and authority, or is listed in `Security:AllowedOrigins`. A missing origin is refused. | **Finding, fixed.** Previously the hub relied only on the `SameSite` cookie policy. Covered by `HubOriginPolicyTests`. |
| 9 | Uploads and knowledge corpus | There are no file uploads. Knowledge documents are text forms with a 100,000-character body limit and a 200-character title limit. Each organization may hold at most 5,000 passages (about 4 MB of text at 800 characters per passage). | **Finding, fixed.** The corpus had no total limit. The cap is enforced in `KnowledgeDocument` and reported to the user. |
| 10 | Feed payloads | Live payloads are capped at 5 MB while they are read. Invalid rows are rejected one at a time. | Fixed (the cap is new). |
| 11 | Feed keys | Feed keys are sent as bearer tokens only over https. Error messages do not include the key or the response body. Startup rejects an unencrypted feed endpoint. | Fixed (startup validation is new). Covered by tests. |
| 12 | Model key | The model endpoint was accepted over plain http, so its API key could be sent in the clear. | **Finding, fixed.** `AiOptionsValidator` now requires https, or http on a loopback host, and validates on startup. |
| 13 | Client address | The sign-in limit used the connection's remote address. Behind a reverse proxy, every client would share the proxy's address. | **Finding, fixed.** Forwarded headers are trusted only from loopback and from `Security:TrustedProxies`. |
| 14 | Narrative cache | Narratives are cached under a hash of the organization, the model version, the prompt, and the evidence. Cache failures never fail a request. | **Finding, fixed.** The key used to omit the organization, so two tenants with identical evidence shared an entry. `IdenticalEvidence_ForAnotherOrganization_IsNeverServedFromTheFirstTenantsEntry` covers this. |
| 15 | Container | The runtime image runs as the non-root `app` user. | Verified in the `Dockerfile`. |
| 16 | Errors | Developer exception pages only in Development. Production uses the exception handler. | Verified in `Program.cs`. `AMissingPage_CarriesTheSecurityHeaders_AndRevealsNoDetails` checks that a missing page returns 404 without stack traces. |
| 17 | Dependencies | The vulnerable-package audit runs in CI before the build. | Verified in `ci.yml`. |
| 18 | Logging of secrets | No logger call or error message writes an API key. | Verified by searching the source for key references in log and error paths. |
| 19 | Load and soak | CI: 20 requests per second for two minutes against the production container. Nightly: a 30-minute constant-rate soak with the same thresholds. Failure rate, latency, and check thresholds fail the job. | Added (`tests/load/aurora-smoke.js`). The soak is 30 minutes, not multi-hour. |
| 20 | Cross-origin isolation | `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Resource-Policy: same-origin`, and `Cross-Origin-Embedder-Policy: require-corp` on every page. | **Finding, fixed.** The baseline scan reported these as missing (ZAP 90004). `Pages_AreCrossOriginIsolated` covers them. |
| 21 | Sign-in return URL | The login `ReturnUrl` is redirected only when `Url.IsLocalUrl` accepts it, and it is HTML-encoded in the form. | Verified. The scan still reflects the value into an attribute (ZAP 10031), which is accepted with the reason below. |
| 22 | Passive DAST | OWASP ZAP baseline on every CI run. Its findings are published as annotations. | Run (CI `38512399065`, passed). Only informational alerts, each accepted below. |
| 23 | Active DAST | OWASP ZAP full scan nightly, signed in, against the production container. The scan reached the signed-in pages (checked from the container log). | Run (`38518246188`, passed). Only informational alerts remain. The scan found one real defect, row 24. |
| 24 | Concurrent alert transitions | `POST /Alerts/Acknowledge/{id}` and `POST /Alerts/Resolve/{id}` save an alert read at the start of the request. Two concurrent requests for the same alert made the second save fail on the row version. The failure was not caught. | **Finding, fixed.** The scan reported a 500 from `Resolve` (ZAP 90022). The save now reports "The alert changed while you were working on it" and the audit row of the losing request is not written. CI builds and tests the change. The conflict path has no dedicated test. The scan confirmed the 500 is gone. |

## Findings and what changed
- **Model key over plain http (12).** A deployment could have sent the model key without encryption. Startup now refuses
  such a configuration, unless the host is loopback, where the key does not leave the machine. Tests cover both cases.
- **Shared client address behind a proxy (13).** The login limit could have locked out every user of a deployment at
  once, or let one client avoid it by changing headers. Only configured proxies may now supply the address.
- **Cross-site WebSocket (8).** The hubs now refuse any connection whose origin is not this application's own origin
  or an entry in `Security:AllowedOrigins`.
- **Narrative cache shared across tenants (14).** The organization is now part of the cache key, so one tenant's cached narrative is never served to another. This removes the cross-tenant existence check that was previously listed as a residual risk.
- **Session lifetime (1).** Sliding renewal is off. A session ends 8 hours after sign-in.
- **Knowledge corpus size (9).** Each organization is capped at 5,000 passages.
- **Cross-origin isolation headers (20).** Added after the baseline scan.
- **Feed endpoint validation (11) and payload cap (10).** Added with the live feed adapters.

## Scan alerts and their dispositions
The passive and signed-in active scans now report only informational alerts. Each is accepted for the reason given.

| ZAP alert | Level | Disposition |
|-----------|-------|-------------|
| 90022 Application Error Disclosure (`POST /Alerts/Resolve/{id}`, HTTP 500) | Low | **Fixed**, row 24. This was a real defect. |
| 10031 User Controllable HTML Element Attribute (`POST /Assets/Create`, `GET /Knowledge?q=`, sign-in `ReturnUrl`) | Informational (confidence low) | Accepted. The values are echoed into `value="..."` attributes, and Razor HTML-encodes every one of them. The sign-in `ReturnUrl` is also redirected only when `Url.IsLocalUrl` accepts it. |
| 90003 Information Disclosure - Suspicious Comments (`aurora.simulation.js`) | Informational (confidence medium) | Accepted. The comments describe the progress bar's behaviour. They contain no credentials, paths, or internal details. |
| 10049 Storable and Cacheable Content (`robots.txt`, `/Account`) | Informational (confidence medium) | Accepted as informational. `robots.txt` is public. The scan flags whether a response may be cached, not what it contains, and the `/Account` response it reports shows no user data in its evidence. Setting `Cache-Control: no-store` on every page that shows user data is a candidate follow-up. |
| 10049 Non-Storable Content | Informational (confidence medium) | Informational only. It confirms that the pages are not cached. |
| 10104 User Agent Fuzzer | Informational (confidence medium) | Accepted as informational. The scanner reports that some responses differ by user agent. It is a difference in responses, not a vulnerability. |
| 90027 Cookie Slack Detector | Informational (confidence low) | Accepted as informational. It lists cookie names observed on the signed-in forms. It is not a vulnerability. |
| 10111 Authentication Request Identified | Informational (confidence high) | Accepted. It identifies the login form. The login form is expected to be a target. |
| 10112 Session Management Response Identified | Informational (confidence medium) | Accepted. It identifies the response that sets the session cookie. This is expected. |

## Residual risks, accepted
- **No external penetration test.** This review, the regression tests, and the automated scans have not replaced a
  third-party test. One has not been commissioned.
- **Live feeds and model endpoints were not exercised.** Their fixtures and stubs are tested. A live provider's behaviour,
  including its error responses, is unverified until its opt-in test runs with real credentials. `live-tests.yml`
  reports the gap as a warning while the endpoint secrets are absent. It does not fail.
- **The signed-in scan uses one account, the administrator.** It reaches the pages the administrator can open, and it
  does not test the boundaries between roles. Those are covered by the HTTP regression tests, not by the scanner. The
  scan also writes rows into its disposable database.
- **The soak is 30 minutes.** Nightly CI runs a 30-minute constant-rate soak. A multi-hour soak in a dedicated
  environment has not been run.
- **Session lifetime of 8 hours.** A stolen session stays valid for up to 8 hours after sign-in. Shorten
  `ExpireTimeSpan` if the risk appetite requires it.
- **Client address trust depends on configuration.** If the reverse proxy is not listed in `Security:TrustedProxies`,
  the limit applies to the proxy's address. This is a deployment requirement, documented in the README.
- **Scheduled scans run only from the default branch.** GitHub runs `schedule` and `workflow_dispatch` workflows only from
  the default branch. The nightly soak, scan, and live workflow run from the default branch once this change is merged.
