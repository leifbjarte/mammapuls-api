# Squad Decisions

## Active Decisions

### 2026-09-29T15:23:39+02:00: Stack and platform
**By:** Leif Bjarte Johansson (via Copilot)
**What:** C# on the latest .NET (latest overall, not only LTS), .NET Aspire, ASP.NET Core minimal APIs. Code on GitHub, deployment to Azure.
**Why:** User request

### 2026-09-29T15:23:39+02:00: Always use the cheapest Azure resources
**By:** Leif Bjarte Johansson (via Copilot)
**What:** Very low-budget API. Always choose the cheapest viable Azure resource/SKU. Region choice must also account for Azure AI Foundry model availability.
**Why:** User request

### 2026-09-29T15:23:39+02:00: Authentication via Vipps Login
**By:** Leif Bjarte Johansson (via Copilot)
**What:** Use Vipps Login (OIDC) for authentication — https://developer.vippsmobilepay.com/docs/APIs/login-api/
**Why:** User request

### 2026-09-29T15:23:39+02:00: Data scope
**By:** Leif Bjarte Johansson (via Copilot)
**What:** API will hold personalized data now, audio and video files in the future. Current scope: scaffolding only.
**Why:** User request

### 2026-09-29T15:23:39+02:00: Solution scaffold conventions
**By:** Han (Backend Dev), requested by Leif Bjarte Johansson
**What:**
- Target `net10.0` (SDK 10.0.301, latest installed). Aspire 13.5.4 (`Aspire.AppHost.Sdk/13.5.4`, `AspireUseCliBundle=true`).
- Solution file is `Mammapuls.slnx` (XML format) at repo root. Projects under `src/`: `Mammapuls.AppHost`, `Mammapuls.ServiceDefaults`, `Mammapuls.Api`. Future tests go under `tests/`.
- Central Package Management: all versions in `Directory.Packages.props`; `PackageReference` items carry no `Version`.
- `Directory.Build.props` sets TFM, Nullable, ImplicitUsings, and `TreatWarningsAsErrors=true` (NuGet audit warnings NU1901–NU1904 stay warnings so a new advisory doesn't break CI).
- API routes are versioned under `/api/v1`; one static `*Endpoints` class per feature under `src/Mammapuls.Api/Endpoints/`. Errors are ProblemDetails (`AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`).
- OpenAPI doc (`/openapi/v1.json`) and `/health`, `/alive` are mapped only in Development.
- AppHost resource name for the API is `api`. `aspire.config.json` lives at repo root.
- `UseHttpsRedirection` intentionally omitted — TLS expected to terminate at the Azure ingress (Lando to confirm).
**Why:** Consistent layout and versioning for the other agents (infra, auth, tests) to build on.

### 2026-09-29T15:41:34+02:00: ACCEPTED — Azure architecture (Leia's proposal, with Fact Checker corrections)
**By:** Leif Bjarte Johansson (approved via Copilot)
**What:**
- Single region Sweden Central. Deploy with `aspire deploy` (not azd).
- API on Azure Container Apps consumption, scale-to-zero (cold start accepted). No private endpoints, no planned maintenance, dashboard disabled in Azure unless needed.
- Cosmos DB free tier (must be enabled at account creation; override Aspire's serverless default). Cosmos emulator locally.
- Blob Storage LRS, private, short-lived SAS/user-delegation for media. CDN/Front Door deferred.
- Foundry: Data Zone Standard (EU) for any prompt containing user data. Audio realtime/transcribe models are Global-only — not allowed with personal data; only regional whisper/tts/tts-hd if audio is needed.
- ACR Basic. Log Analytics daily cap 0.15 GB/day, 30-day retention, trace sampling. No Key Vault for now; managed identity everywhere.
- Budget alert required before the first deploy; verify per-meter cost soon after (undocumented ACA "Environment Management Hour" meter).
**Why:** User approved; cheapest-resources directive.

### 2026-09-29T15:41:34+02:00: ACCEPTED — Vipps Login architecture and product answers
**By:** Leif Bjarte Johansson (approved via Copilot)
**What:**
- API is the confidential OIDC client (code flow + PKCE, `response_mode=query`, handle `client_secret_basic`) and issues its own HttpOnly/Secure/SameSite=Lax cookie. Vipps tokens are never accepted by API endpoints. `/auth/logout` signs out only our cookie.
- Website is a single-page app on the same site (e.g. `mammapuls.no` + `api.mammapuls.no`) — cookie approach confirmed; CORS with credentials for the SPA origin.
- Scopes: `openid name email phoneNumber` (user wants name, email, phone; Vipps scope is `phoneNumber`, claim is `phone_number` — corrected by Ackbar). Consent screen will appear. Store only these claims; users keyed on (issuer, `sub`).
- No Vipps merchant yet — user will create one; develop against the Vipps test environment until then.
**Why:** User answers 2026-09-29.

### 2026-09-29T15:41:34+02:00: Stay on .NET 10 for now
**By:** Leif Bjarte Johansson (via Copilot)
**What:** Target `net10.0` for now; revisit when .NET 11 is GA. Supersedes "latest overall" for the time being.
**Why:** User request

### 2026-09-29T15:23:39+02:00: PROPOSAL — Vipps Login integration
**By:** Ackbar
**Status:** Accepted 2026-09-29 by Leif Bjarte Johansson — see ACCEPTED entries; Fact Checker corrections apply
**Sources:** Vipps MobilePay Login API docs (API guide: core concepts, Login from a website, Userinfo, Webhooks, Important information, FAQ; Test environment), read 2026-09-29.

#### Facts from the docs (verified)
- Discovery — Test: `https://apitest.vipps.no/access-management-1.0/access/.well-known/openid-configuration`; Prod: `https://api.vipps.no/access-management-1.0/access/.well-known/openid-configuration`. Cache it (the docs say `max-age=3600`).
- Flow for websites: authorization code, redirect only (no iframe/popup). PKCE is supported (`S256`). `state` must be ≥ 8 chars. `response_modes_supported` = `query`, `fragment` (**no `form_post`**).
- Token endpoint auth: the default is `client_secret_basic`. You can switch to `client_secret_post` on the business portal (the setting applies per sales unit).
- **The Vipps access token is an opaque random string, and it is used only to call `userinfo`.** It expires after 10 min. **There are no refresh tokens.** The ID token is an RS256 JWT, and you verify it against `jwks_uri`.
- The `openid` scope is required, needs no consent, and gives only `sub`. If you request only scopes that need no consent, the consent screen is skipped. The user must accept all requested scopes or none. The basic plan has `name`, `phoneNumber`, `email` and `address`. `nin` is restricted and ends for Norway on 2027-01-01.
- `sub` is unique **per sales unit (MSN)**. It changes only if the user deletes their Vipps profile. Test and prod are different sales units, so they give different `sub` values.
- Redirect URIs must match the portal **exactly**. `localhost` is allowed. Test and prod use separate API keys.
- Vipps does not support merchant-initiated logout. Log the user out of **our** session only.
- The `CONSENT_REVOKED` webhook is optional. Its body is a JWT with **`alg: none`** (unsigned) that carries `sub`.
- For GDPR, the merchant is an **independent data controller** for the data it receives and needs its own legal basis. Users must be at least 15.

#### 1. Architecture — recommend: API is the confidential OIDC client and issues its own cookie session
- A third-party resource server cannot validate Vipps access tokens (they are opaque and only for userinfo). The ID token's audience is our client, so it must not be used as an API bearer token. **The API must therefore not accept Vipps tokens as bearer credentials.**
- Recommendation (cheapest, fewest moving parts): the API itself runs code flow + PKCE as a confidential client. It then issues an **HttpOnly, Secure, SameSite=Lax cookie** that holds only the internal user id. The website calls the API with credentials.
  - This needs the website and the API on the **same site**, e.g. `mammapuls.no` + `api.mammapuls.no`, or the website reverse-proxying `/api`. Otherwise browsers block the cookie. Container Apps custom domains and managed certs are free. UNVERIFIED: whether `*.azurecontainerapps.io` is on the Public Suffix List (assume yes, so use custom domains).
  - Harden with CORS allowlisting the exact site origin plus `AllowCredentials`, and require a custom header (e.g. `X-Requested-With`) or antiforgery on state-changing requests.
- Alternative if the website is server-rendered with its own backend: the website becomes the BFF and runs Vipps login. The API then needs its own trust mechanism (Entra-issued or self-issued JWTs). **Not recommended.** It means more keys, more cost and more code.
- Rejected: issuing our own JWTs to the browser (key management, refresh, XSS token theft).

#### 2. ASP.NET Core wiring (API)
- `AddAuthentication(Cookie default, OpenIdConnect challenge)`.
- OIDC options:
  - `Authority` = `https://apitest.vipps.no/access-management-1.0/access/` (test) or `https://api.vipps.no/access-management-1.0/access/` (prod).
  - `ResponseType = code`, **`ResponseMode = query`** (the handler default `form_post` is unsupported), `UsePkce = true`.
  - `SaveTokens = false`, `MapInboundClaims = false`, `CallbackPath = /signin-vipps`.
  - `Scope = { "openid" }` to start.
  - `GetClaimsFromUserInfoEndpoint = false` unless extra scopes are added.
- Validation: issuer is taken from discovery. The test issuer is verified; the prod issuer string is UNVERIFIED but comes from discovery. Also: `ValidAlgorithms = RS256`, lifetime, and nonce (the handler does this). Set `ValidAudience = ClientId`. UNVERIFIED: one older doc example shows `aud: ["vipps-integration"]` — confirm in MT before locking down.
- Client auth: ASP.NET Core's handler sends the secret in the form body. Either set the portal to `client_secret_post` (documented), **or** hook `OnAuthorizationCodeReceived` to send a Basic header. `private_key_jwt` appears in discovery, but its portal availability is UNVERIFIED.
- In `OnTokenValidated`: look up or create the user by **(issuer, sub)**. Then replace the principal with a minimal identity: internal `uid` plus `sub`. Nothing else goes in the cookie.
- Cookie: API routes return 401/403 instead of redirecting (`OnRedirectToLogin`/`OnRedirectToAccessDenied`). Session lifetime is ours, since there is no refresh token; suggest 8 h sliding / 14 d absolute.
  - Data Protection keys: persist them to Blob Storage before prod, otherwise restarts log users out. Key Vault key wrapping is optional (low cost).
- Endpoints:
  - `GET /auth/login?returnUrl=` (allowlist returnUrl → no open redirect)
  - `/signin-vipps` (handler)
  - `POST /auth/logout` (local sign-out only)
  - `GET /auth/me`
- Fallback authorization policy = authenticated; anonymous endpoints opt out explicitly.
- Local dev: pin the API's HTTPS port in the AppHost so `https://localhost:{port}/signin-vipps` is a stable, registerable redirect URI on the test sales unit.

#### 3. Secrets
- ClientId is non-secret config. ClientSecret is a secret, and each environment has its own pair.
- Local: AppHost `AddParameter("vipps-client-secret", secret: true)`, stored in **user-secrets**, passed to the API via env/config.
- Azure (cheapest): **Container Apps secrets** (free), populated from the Aspire secret parameter at deploy (azd). UNVERIFIED: exact azd/Aspire behaviour per version — Lando to confirm.
- Key Vault (Standard, managed identity) only when rotation or multiple secrets justify it.
- Never put secrets in appsettings, the repo, logs or `.squad/`.

#### 4. GDPR
- Minimize: request `openid` only, and store `sub` + internal id + created/last-login. Add `name`/`email` only for a documented purpose.
- Never request `nin`. Don't persist Vipps tokens or raw userinfo.
- `sub` is pseudonymous personal data: log the internal id only, never tokens, `sub` or claims.
- Deletion: `DELETE /me` removes the user row and all linked data (later also media blobs).
- `CONSENT_REVOKED` webhook: the payload is unsigned. UNVERIFIED how the webhook itself is authenticated ("specific authentication requirements" is mentioned but not detailed in the pages I read). Treat it as a hint only: flag the user and require re-login, and don't auto-delete on unauthenticated input.
- Keep all data in an EU region. A privacy policy and legal basis (terms/contract) are needed before prod.

#### 5. Scaffolding split
- **Han, now:**
  - `Authentication:Vipps` config section (`Authority`, `ClientId`, `CallbackPath`, `Scopes`) with test values in Development only.
  - AppHost secret parameter placeholder.
  - Cookie + OIDC wiring as above.
  - `/auth/*` stubs, fallback auth policy, and a `User` entity with a unique (issuer, sub) index.
  - No real client id/secret committed.
- **Later:** real test sales unit keys, prod keys, custom domains, Data Protection persistence, the consent webhook, `DELETE /me`, extra scopes, and in-app confirmation (`acr`, advanced tier only).

#### Open questions for user
1. Is there a Vipps MobilePay merchant agreement with Login ordered, a test sales unit and a plan tier?
2. Is the website an SPA, server-rendered, or a static site? Which host?
3. Can the website and the API share one registrable domain (custom domain)?
4. Which user data is actually needed beyond a stable id (display name? email for notifications?)

### 2026-09-29T15:23:39+02:00: PROPOSAL — Azure architecture & cost
**By:** Leia
**Status:** Accepted 2026-09-29 by Leif Bjarte Johansson — see ACCEPTED entries; Fact Checker corrections apply
**Requested by:** Leif Bjarte Johansson

Prices are USD retail (Azure Retail Prices API, `prices.azure.com`, region `swedencentral`, fetched 2026-09-29) unless marked **UNVERIFIED**. Month is 730 h. Free grants are from Microsoft Learn docs.

---

#### 0. Region (overall) — **Sweden Central (`swedencentral`)**

| Region | EU/EEA data | Foundry Data Zone (EU) model coverage | ACA vCPU active /s | Blob Hot LRS /GB·mo | Verdict |
|---|---|---|---|---|---|
| **Sweden Central** | EU | Broadest in EU: gpt-4.1*, gpt-4o*, gpt-5/-mini/-nano, 5.1, 5.4(-mini), 5.5, 5.6*, gpt-6-luna/sol, o-series, embeddings, model-router, gpt-image-1.5. Also the EU home for audio (gpt-4o-transcribe, tts, realtime, whisper) | $0.000024 | $0.0184 | **Pick** |
| Norway East | EEA (in EU Data Boundary) | DZ lacks gpt-4.1*, gpt-5-mini/nano, **all embeddings**, o-series | $0.000024 | $0.0216 | Reject: weak model coverage |
| West Europe | EU | Good DZ coverage, no model-router/image | $0.000034 | $0.0196 | Reject: ~40% pricier ACA |
| Germany West Central | EU | Good DZ coverage | $0.000024 | not checked | Viable fallback |

Everything in one region avoids inter-region transfer ($0.02/GB) and keeps personal data in the EU.

---

#### 1. API hosting — **Azure Container Apps, Consumption, scale-to-zero**

| Option | Fixed cost/mo | Free grant | Notes |
|---|---|---|---|
| **ACA Consumption** (Aspire `AddAzureContainerAppEnvironment`) | **$0** at zero replicas | 180,000 vCPU-s + 360,000 GiB-s + 2M requests per subscription/month | Active $0.000024/vCPU-s, $0.000003/GiB-s, idle $0.000003; requests $0.40/M after free. Aspire default is a Consumption workload profile. Cold start on first request after idle |
| App Service F1 (Linux) | $0 | — | 60 CPU-min/day, no Always On, no custom domain (**UNVERIFIED** current limits). Not viable for production |
| App Service B1 (Linux) | $13.14 | — | $0.018/h, always on |
| App Service P0v3 (Linux) — **Aspire's App Service default** | $64.97 | — | $0.089/h. Too expensive |

Sizing example: 0.25 vCPU / 0.5 GiB, busy 4 h/day, is about 108k vCPU-s and 216k GiB-s. That fits inside the free grant, so it costs **$0**. An always-warm option (`minReplicas=1`, idle rate) would cost **about $4.30/mo**. That figure assumes the free grant also covers idle usage, which is **UNVERIFIED**.

⚠️ Watch out for the ACA "Environment Management Hour" meter: $0.13/h (about $95/mo), effective 2026-09-01. The billing docs say private endpoints and planned maintenance trigger this charge even on Consumption. **Don't enable either one.**

**Recommendation:** ACA Consumption with `minReplicas=0`, 0.25 vCPU / 0.5 GiB, and `maxReplicas=2` as a cost ceiling.

---

#### 2. Personalized data store — **Cosmos DB for NoSQL, lifetime free tier**

| Option | Free allowance | Cost at idle / low traffic | Catch |
|---|---|---|---|
| **Cosmos DB free tier** (provisioned) | 1000 RU/s + 25 GB, for the account's lifetime | **$0** | One free-tier account per subscription. Can't be combined with serverless. **Aspire's default is serverless**, so override with `ConfigureInfrastructure`: remove `EnableServerless`, set `EnableFreeTier=true`, and give the database shared throughput of ≤1000 RU/s. Over the RU limit you get 429s, not a bigger bill |
| Azure SQL DB free offer (**Aspire's default**: GP_S_Gen5_2, `useFreeLimit`, AutoPause) | 100,000 vCore-s + 32 GB per DB, up to 10 DBs | $0 | Min auto-pause delay is 15 min at 0.5 vCore, so each wake-up costs ≥450 vCore-s: at most ~220 wake-ups/month. With daily scattered traffic the DB **pauses until next month** (outage) or starts billing. Resume latency on every wake |
| Cosmos DB serverless (Aspire default) | none | pay per RU (**UNVERIFIED**, no retail price found) | Cheap, but not $0 |
| Table Storage | none | cents (**UNVERIFIED**) | Weak querying; poor fit for evolving user profiles |
| PostgreSQL Flexible B1ms | none | $14.53 compute + 32 GB × $0.1369 = **≈ $18.90** | Always on |

**Local dev:** `AddAzureCosmosDB("cosmos").RunAsPreviewEmulator(e => e.WithDataExplorer())`, the Linux emulator (experimental, `ASPIRECOSMOSDB001`). `RunAsEmulator()` is the stable fallback. Only the deployed environment uses the free-tier account.

**Recommendation:** use the Cosmos free tier with Entra-only auth (Aspire default `disableLocalAuth: true`), single region, no zone redundancy, partitioned by the Vipps `sub`.

---

#### 3. Media (audio/video, later) — **Blob Storage StorageV2, LRS, Hot, private**

| Item | Price | Recommendation |
|---|---|---|
| Hot LRS stored | $0.0184/GB·mo | Default tier for active media |
| Cool LRS stored | $0.01/GB·mo, $0.01/GB retrieval, 30-day minimum (early-delete fee) | Add a lifecycle rule later: move to Cool after N days without access |
| Write / read ops (Hot) | $0.05 / $0.004 per 10k | Negligible |
| Internet egress | First 100 GB/mo free, then ≈ $0.087/GB | **Egress will dominate video costs.** Monitor it |
| Access | — | Public access off. Serve via short-lived **user-delegation SAS** issued by the API with managed identity (no account keys) |
| CDN / Front Door | Front Door Standard has a monthly base fee (**UNVERIFIED** amount) | **Defer** until egress or latency justifies it |
| Media streaming | Azure Media Services is retired (**UNVERIFIED** date: June 2024) | **Defer.** Pre-encode (HLS/MP4) offline and serve from Blob |

**Local dev:** `AddAzureStorage("storage").RunAsEmulator()` (Azurite) plus `.AddBlobs("media")`.

---

#### 4. Azure AI Foundry — **Sweden Central, Data Zone Standard (EU)**

| Deployment type | Where prompts are processed | Price | Use for |
|---|---|---|---|
| Global Standard | Any Azure region | Base (lowest) | Only non-personal content |
| **Data Zone Standard (EU)** | Within the EU Data Boundary (includes Norway/EFTA) | ≈ **+10% vs Global**. Verified on the gpt-5-mini priority-processing meters: in $0.495 vs $0.45, out $3.96 vs $3.60 per 1M tokens. Standard per-token prices are **UNVERIFIED** | **Anything that includes user data (default)** |
| Standard (regional) | Sweden only | **UNVERIFIED** | Few models (gpt-4.1(-mini), gpt-4o-mini, gpt-5.1, o4-mini, whisper, tts). Only if strict single-country residency is ever required |
| Provisioned / PTU | — | Reserved capacity | **Never** at this budget |

Data at rest stays in the Europe geography for all types (Learn). The Foundry resource itself has no fixed cost: pay-per-token only, $0 at idle. Start with a small model (e.g. `gpt-5-mini` or `gpt-5.4-mini`, both DZ-available in Sweden Central). Set a low TPM quota per deployment as a spend cap. Justify each call path before adding it.

---

#### 5. Supporting pieces

| Piece | Choice | Cost/mo | Notes |
|---|---|---|---|
| Container registry | **ACR Basic** (Aspire ACA default) | **$5.07** ($0.1666/day), 10 GiB included | The only unavoidable fixed cost. GHCR could be $0, but you'd lose managed-identity pull and the Aspire-native flow. Revisit only if needed |
| Log Analytics / App Insights | Workspace `PerGB2018` (Aspire default) | **$0** if kept under 5 GB/mo | First 5 GB/mo free, then $2.99/GB. 31 days retention included. **Aspire sets no daily cap**, so add `workspaceCapping.dailyQuotaGb = 0.15` and retention 30 d via `ConfigureInfrastructure`. Use OTel trace sampling (e.g. 10–25%) in ServiceDefaults. Health probes and Info logs stay out of the workspace |
| Aspire dashboard in ACA | Aspire default component | **UNVERIFIED** | Keep for now. Drop it if it shows up on the bill |
| Key Vault | **Not now** | $0 (Standard is $0.03/10k ops if added) | The Vipps client secret goes in as an Aspire `secret: true` parameter, which becomes an ACA secret. Add Key Vault when we need rotation or more secrets |
| Identity | User-assigned managed identity (Aspire default) | $0 | AcrPull, Cosmos data contributor, Storage Blob Data Contributor (+ Delegator for SAS), Cognitive Services OpenAI User. No keys or connection strings with secrets |

---

#### 6. Estimated monthly cost (Sweden Central, USD)

| Scenario | ACA | ACR | Cosmos | Blob | Logs | Foundry | **Total** |
|---|---|---|---|---|---|---|---|
| **Idle** (scale-to-zero, a few GB of data) | $0 | $5.07 | $0 | <$0.10 | $0 | $0 | **≈ $5–6** |
| **Low traffic** (<2M req, 50 GB Hot media, <100 GB egress, <5 GB logs) | $0–1 | $5.07 | $0 | ≈ $1 | $0 | tokens | **≈ $6–8 + AI tokens** |
| + always-warm API (`minReplicas=1`) | +≈ $4.30 (**UNVERIFIED** grant use) | | | | | | **≈ $10–12 + tokens** |

For comparison, the Aspire defaults would cost far more: App Service P0v3 is $65/mo on its own, and PostgreSQL B1ms is ≈ $19/mo.

---

#### Decisions needing user approval

1. **Region:** Sweden Central for everything.
2. **Hosting:** ACA Consumption, scale-to-zero (accept cold starts), and not App Service.
3. **Data:** Cosmos DB NoSQL **free tier**, which means overriding Aspire's serverless default. Azure SQL free offer is rejected because of the auto-pause-until-next-month risk. Note the constraint: only one free-tier account per subscription, so any extra cloud environment (dev/test) either uses a separate subscription or pays.
4. **Foundry:** Data Zone Standard (EU) as the default for user-related prompts. Global Standard only for provably non-personal calls.
5. **Fixed spend:** accept ACR Basic at ≈ $5/mo as the baseline.
6. **Observability caps:** Log Analytics daily cap 0.15 GB, 30-day retention, trace sampling.
7. **No Key Vault, CDN, Front Door, private endpoints, or ACA planned maintenance** for now.
8. **Deploy tool:** the request says `azd`. Current Aspire (13.x) recommends Aspire-native `aspire deploy` from the AppHost, and azd still works but uses different resource naming (`WithAzdResourceNaming`). Pick one before Lando builds the pipeline. I recommend `aspire deploy`.

**Handoffs after approval:** Lando (AppHost `ConfigureInfrastructure` overrides and the GitHub Actions deploy), Ackbar (Vipps secret handling, SAS policy, Foundry data-handling review), Han (Cosmos and Blob clients).

### 2026-09-29T15:23:39+02:00: Verification — Azure architecture & Vipps Login proposals
**By:** Fact Checker
**Requested by:** Leif Bjarte Johansson
**Mode:** Verification (+ short Devil's Advocate per proposal)
**Inputs:** `decisions/inbox/leia-azure-architecture.md`, `decisions/inbox/ackbar-vipps-login.md`
**Sources fetched:** 2026-09-29 (Microsoft Learn, azure.microsoft.com pricing, prices.azure.com retail API, aspire.dev, developer.vippsmobilepay.com, live Vipps discovery documents)

---

#### A. Leia — Azure architecture & cost

| # | Claim | Rating | Evidence |
|---|---|---|---|
| A1 | ACA "Environment Management Hour" is $0.13/h (about $95/mo), effective 2026-09-01 | ✅ Verified (price/date) | The retail API has `meterName: "Environment Management Hour"` in `swedencentral`: retailPrice 0.13, unit 1 Hour, effectiveStartDate 2026-09-01. Source: https://prices.azure.com/api/retail/prices?$filter=serviceName eq 'Azure Container Apps' and armRegionName eq 'swedencentral' |
| A1b | Private endpoints and planned maintenance trigger **this** meter, even on Consumption | ⚠️ Partly correct: needs a naming correction | The retail API lists **three separate** $0.13/h meters, all effective 2026-09-01: `Environment Management Hour`, `Environment Private Endpoint`, `Environment Planned Maintenance Hour`. Learn still describes these as the "Dedicated Plan Management" charge. It applies "regardless of whether you use the Consumption or Dedicated plans", and the charges are **additive per feature** (https://learn.microsoft.com/en-us/azure/container-apps/billing, https://learn.microsoft.com/en-us/azure/container-apps/planned-maintenance). The pricing page still shows Management at $0.10/h (https://azure.microsoft.com/en-us/pricing/details/container-apps/), so the docs lag the meters. Planned maintenance is also "not supported ... on consumption workload profiles". |
| A1c | What triggers the generic `Environment Management Hour` meter | 🔍 Needs Investigation | No Learn page defines it. It could be a renamed Dedicated-plan management fee, or a base fee on every workload-profiles environment. If it is the second, the idle baseline becomes about $100/mo instead of about $5. Resolve this with Cost Management (group by meter) within 48 h of the first deploy. |
| A2 | Consumption free grant: 180,000 vCPU-s + 360,000 GiB-s + 2M requests per subscription per month; $0 when scaled to zero | ✅ Verified | Learn billing: "When a revision is scaled to zero replicas, no resource consumption charges are incurred"; health probes aren't billable. Rates: active $0.000024/vCPU-s and $0.000003/GiB-s, idle $0.000003, requests $0.40/M (retail API + pricing page). Sizing math (108k vCPU-s / 216k GiB-s) checks out. |
| A2b | Free grant also covers idle usage (the always-warm ≈ $4.30) | ⚠️ Unverified | The docs don't split the grant by active/idle. Recomputed: $4.29 if the grant covers idle, $5.91 if it doesn't. The conclusion holds either way. |
| A3 | Cosmos free tier: 1000 RU/s + 25 GB, lifetime, one per subscription, not with serverless | ✅ Verified | https://learn.microsoft.com/en-us/azure/cosmos-db/free-tier: "Free tier is currently not available for serverless accounts"; "up to one free tier ... account per an Azure subscription". **Missing from the proposal:** "you can't set it after the account is created." |
| A3b | Aspire default Cosmos provisioning is serverless, so free tier needs a `ConfigureInfrastructure` override | ✅ Verified | Aspire-generated Bicep has `capabilities: [{ name: 'EnableServerless' }]` and `disableLocalAuth: true` (https://aspire.dev/integrations/cloud/azure/azure-cosmos-db/azure-cosmos-db-host/). The docs don't confirm whether Aspire's `AddCosmosDatabase` emits a throughput setting 🔍, so Lando must set shared database throughput explicitly. |
| A4 | Azure SQL free offer: 100,000 vCore-s + 32 GB per DB, up to 10 DBs, auto-pause until next month | ✅ Verified | https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer. The wake-cost math holds: min auto-pause delay is 15 min for GP and default min vCores is 0.5, so 0.5 × 900 s = 450 vCore-s and about 222 wake-ups/month (https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-overview). "Aspire default GP_S_Gen5_2 + useFreeLimit" was not checked ⚠️. |
| A5 | ACR Basic ≈ $5.07/mo in Sweden Central, and it's the Aspire ACA default | ✅ Verified | Retail API `Basic Registry Unit` = $0.1666/day (×30.4 = $5.07). Aspire ACA env Bicep uses `sku: { name: 'Basic' }` (https://aspire.dev/integrations/cloud/azure/configure-container-apps/). |
| A6 | Sweden Central has the broadest EU Data Zone Standard coverage. Norway East DZ lacks gpt-4.1*, gpt-5-mini/nano, all embeddings, o-series | ✅ Verified | Region table (Data Zone Standard, Europe): swedencentral ✅ for gpt-4.1*, 4o*, 5/-mini/-nano, 5.1, 5.4, 5.4-mini, 5.5, 5.6*, 6-luna/sol, gpt-image-1.5, model-router, o-series, embeddings. norwayeast lacks all of those and also gpt-4o*, gpt-5 and model-router. It **does** have gpt-5.4(-mini), 5.5, 5.6*, 6-luna/sol. https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability |
| A6b | Sweden Central is "the EU home for audio" under Data Zone | ❌ Contradicted | **No audio models are offered as Data Zone Standard** in the EU. gpt-4o-transcribe, gpt-4o-mini-transcribe, gpt-realtime* and gpt-audio* are **Global Standard only** in Sweden Central, so processing may leave the EU. EU-resident options are regional **Standard** `whisper`, `tts` and `tts-hd` in Sweden Central. |
| A6c | Regional Standard in Sweden Central = "gpt-4.1(-mini), gpt-4o-mini, gpt-5.1, o4-mini, whisper, tts" | ⚠️ Incomplete | The same page also lists gpt-4o (3 versions), o1, text-embedding-3-large, ada-002 and tts-hd. |
| A6d | Data Zone Standard (EU) keeps processing inside the EU Data Boundary; data at rest stays in the geography | ✅ Verified | https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-models/concepts/deployment-types: "processes data within the Azure EU Data Boundary", which "can include EFTA ... Norway". Caveat: "Microsoft can add regions to either data zone without prior notice." |
| A7 | Aspire recommends `aspire deploy` over azd; azd naming differs (`WithAzdResourceNaming`) | ✅ Verified | https://aspire.dev/deployment/azure/ uses `aspire deploy` as the primary path and sends azd users to "Use existing azd workflows". `WithAzdResourceNaming` is documented on the configure-container-apps page. |
| A8 | Aspire dashboard in ACA cost | ⚠️ Unverified (still) | It's a default `dotNetComponents` resource of type `AspireDashboard`. Learn has no billing statement for it. `.WithDashboard(false)` removes it (https://learn.microsoft.com/en-us/azure/container-apps/aspire-dashboard). |

**Corrections Leia's proposal needs**
1. §1 warning: name the three meters and state that they're additive (PE + PM ≈ $190/mo). Mark the trigger of the generic `Environment Management Hour` meter as **unknown** and add a post-deploy meter check plus an Azure budget alert (e.g. $15/mo).
2. §0 table: remove "EU home for audio" from the Data Zone column. Audio under DZ doesn't exist. For user voice data use regional Standard `whisper` in Sweden Central, or treat the transcribe/realtime models as Global (non-EU) processing.
3. §2: add "free tier must be set **at account creation**; it can't be enabled afterwards". The first `aspire deploy` must already carry the override, otherwise the account has to be deleted and recreated.
4. §4 table: complete the regional-Standard model list, or drop the list and link the source.

---

#### B. Ackbar — Vipps Login

| # | Claim | Rating | Evidence |
|---|---|---|---|
| B1 | Access token is opaque and used only for userinfo; expires in 10 min; no refresh tokens | ✅ Verified | Core concepts: "Access tokens are random strings ... The token itself does not provide any information, but it can be used to fetch ... userinfo"; "Login does not currently support refresh tokens." Userinfo page: "The access token will expire after 10 minutes." https://developer.vippsmobilepay.com/docs/APIs/login-api/api-guide/core-concepts/ |
| B2 | `response_mode` `form_post` is not supported | ✅ Verified | Live discovery (test **and** prod): `"response_modes_supported":["query","fragment"]` |
| B3 | Default token endpoint auth is `client_secret_basic`; `client_secret_post` can be switched on per sales unit in the portal | ✅ Verified | Core concepts, "Token endpoint authentication method" |
| B4 | Discovery URLs (test/prod), `Cache-Control: max-age=3600` | ✅ Verified | Both URLs return valid documents. max-age stated in https://developer.vippsmobilepay.com/docs/APIs/login-api/api-guide/browser-flow-integration/ |
| B5 | Prod issuer string (marked UNVERIFIED in proposal) | ✅ Verified | Live prod discovery: `"issuer":"https://api.vipps.no/access-management-1.0/access/"` |
| B6 | `private_key_jwt` appears in discovery | ❌ Contradicted | Live test and prod discovery: `"token_endpoint_auth_methods_supported":["client_secret_post","client_secret_basic"]`. Only the **stale example** in the website guide lists `private_key_jwt` and `none`. |
| B7 | `openid` is required, needs no consent, and gives only `sub`; consent is all-or-none | ✅ Verified | https://developer.vippsmobilepay.com/docs/APIs/login-api/api-guide/user-info/ ("This view is skipped if no scopes requiring consent are requested"; "must either accept or reject the full set") |
| B8 | `sub` is unique per sales unit (MSN) and changes only if the user deletes their profile | ✅ Verified (with nuance) | "only one `sub` per sales unit"; "Different sales units will get different `sub`s". The docs say "some special cases" and list profile deletion as the example, so it isn't guaranteed to be the only case. |
| B9 | PKCE S256, state ≥ 8 chars, redirect only (no iframe) | ✅ Verified | Discovery `code_challenge_methods_supported: ["S256"]` (docs note the default is `plain` if omitted; ASP.NET sends S256). State ≥ 8 chars and "iFrame is not supported, new window is not recommended" are from the website guide. A popup is "not recommended", not forbidden. |
| B10 | No merchant-initiated logout | ✅ Verified | Live discovery `"end_session_endpoint": ".../access/not-in-use"`, and all logout flags are false |
| B11 | `CONSENT_REVOKED` body is an unsigned JWT (`alg: none`) carrying `sub`; webhook auth not detailed | ✅ Verified | https://developer.vippsmobilepay.com/docs/APIs/login-api/api-guide/webhooks/: "provides no additional security over standard JSON". "Specific authentication requirements" is mentioned but never specified. Ackbar's "hint only" handling is correct. |
| B12 | `aud` may be `["vipps-integration"]` | 🔍 Needs Investigation (leaning resolved) | The older example uses `aud: ["vipps-integration"]`. The newer example (2025 timestamps) has `"aud": "<client id>"` plus `azp`. `ValidAudience = ClientId` is probably right; confirm with a real test token. |
| B13 | Basic plan scopes; `nin` ends for Norway 2027-01-01; min age 15; merchant is an independent controller | ✅ Verified | User-info page and Important-information page |
| B14 | ASP.NET Core OIDC handler sends the client secret in the form body | ✅ Consistent (not re-tested) | This matches the handler's known behaviour. Both proposed mitigations are valid per B3. |

**Corrections Ackbar's proposal needs**
1. Delete the `private_key_jwt` sentence (B6). Only `client_secret_basic` and `client_secret_post` are offered.
2. Mark the prod issuer as verified (B5).
3. Add: the portal has an "ID token includes userinfo" setting. For GDPR minimisation, keep it **off**. With `openid`-only scope nothing extra is shared anyway, but it matters as soon as scopes are added.
4. Add: discovery advertises `end_session_endpoint = .../not-in-use`. `POST /auth/logout` must sign out **only the cookie scheme**. Calling `SignOutAsync` with the OIDC scheme would redirect users to a dead Vipps URL.

---

#### Devil's Advocate

**Leia — biggest risk: the "≈ $5/month" baseline rests on an undocumented meter.**
- *Steelman against:* Microsoft introduced a generic `Environment Management Hour` meter on 2026-09-01 at the same price as the feature meters, and no public doc says what triggers it. If it bills every workload-profiles environment (which Aspire always creates), the idle cost is about $100/mo, 20× the estimate, and Consumption-only ACA loses its cost edge over App Service B1 ($13/mo).
- *Pre-mortem:* In November 2026 the first full invoice shows $95 under "Azure Container Apps – Environment". No budget alert existed.
- *Mitigation:* create an Azure budget alert before the first deploy. Check Cost Management by meter 24–48 h after the deploy. Have a fallback ready: App Service B1, or a classic Consumption-only environment if one is still offered.

**Ackbar — biggest risk: the cookie-session design depends on a same-site custom domain that doesn't exist yet.**
- *Steelman against:* everything in §1 (SameSite=Lax cookie, credentials CORS) fails if the site and the API aren't on the same registrable domain. The website host and domain are still open questions. If the site ends up on a static host with a different registrable domain, the design has to be reworked into a BFF or token-based design.
- *Pre-mortem:* Han scaffolds cookie auth, the frontend ships on `*.azurestaticapps.net`, logins "succeed" but every API call returns 401 in Safari/Firefox because third-party cookies are blocked.
- *Mitigation:* answer Open Question 3 (shared domain) **before** Han builds anything past the `/auth/*` stubs. Keep the fallback (website-as-BFF) documented.

---

#### Recommendation
**Proceed with both, after the listed corrections.** Neither proposal has a blocking factual error. The one material cost unknown (A1c) and the one material residency error (A6b, audio) should be fixed in the proposal text before user approval.

### 2026-09-29T15:41:34+02:00: Azure AppHost model and CI/CD
**By:** Lando (DevOps / Infra), requested by Leif Bjarte Johansson

**Interface contract (implemented in `src/Mammapuls.AppHost/AppHost.cs`):**
- Cosmos: account resource `cosmos`, database `mammapuls`, container connection name **`users`** (partition key `/id`). Env: `ConnectionStrings__users` (`AccountEndpoint=...;Database=mammapuls;Container=users`) plus `USERS_URI`, `USERS_DATABASENAME`, `USERS_CONTAINERNAME`.
- Blob: storage resource `storage`; blob container connection names **`media`** and **`dataprotection`** (`ConnectionStrings__media` / `ConnectionStrings__dataprotection` = `Endpoint=<blob endpoint>;ContainerName=<name>`).
- Parameters → env vars on `api`: `vipps-client-id` → `Authentication__Vipps__ClientId`; `vipps-client-secret` (secret) → `Authentication__Vipps__ClientSecret` (ACA secret ref); `spa-origin` → `Cors__AllowedOrigins__0`.
- `api` is external (`WithExternalHttpEndpoints`); TLS terminates at ACA ingress and `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set by Aspire. **Confirms Han's choice to omit `UseHttpsRedirection`.**

**Run mode:** Cosmos Linux preview emulator (`RunAsPreviewEmulator` + Data Explorer, experimental `ASPIRECOSMOSDB001`; fall back to `RunAsEmulator()` if the client has trouble). Azurite for storage. Parameters must be set locally (user secrets `Parameters:vipps-client-id` etc.) or the dashboard prompts.

**Publish mode (Sweden Central, default in AppHost `appsettings.json` `Azure:Location`):**
- ACA env `aca`: Consumption profile, **dashboard disabled**, no private endpoint / planned maintenance, ACR **Basic** (Aspire default).
- `api`: min 0 / max 2 replicas, 0.25 vCPU / 0.5 GiB.
- Cosmos: `enableFreeTier: true`, serverless capability removed, shared DB throughput **1000 RU/s** on `mammapuls`. `disableLocalAuth: true` (Entra only). **Any new Cosmos database must share this throughput or it leaves the free tier.**
- Storage: Standard_LRS, `allowBlobPublicAccess: false`, `allowSharedKeyAccess: false` (Aspire default → **account-key SAS is impossible; use user-delegation SAS**).
- `api` managed identity gets Storage Blob Data Contributor **+ Storage Blob Delegator** (for user-delegation SAS) and Cosmos data contributor.
- Log Analytics: 30-day retention, daily cap 0.15 GB.

**For Ackbar/Han:** Aspire emits `runtime.dotnet.autoConfigureDataProtection: true` on the Container App. If the API persists keys to the `dataprotection` blob container explicitly, verify there is no conflict after first deploy.

**CI/CD:** `.github/workflows/ci.yml` (build + test on push/PR to main). `.github/workflows/deploy.yml` (manual, GitHub Environment `production`, OIDC via `vars.AZURE_CLIENT_ID/AZURE_TENANT_ID/AZURE_SUBSCRIPTION_ID`, `aspire deploy` pinned to CLI 13.5.4). Needs `vars.AZURE_RESOURCE_GROUP`, `vars.VIPPS_CLIENT_ID`, `vars.SPA_ORIGIN`, `secrets.VIPPS_CLIENT_SECRET`.

**Budget:** not modelled in the AppHost (must exist before the first deploy; subscription scope preferred; CI identity would need Cost Management rights). Created manually with a one-time `az rest` call — see Lando's report.

### 2026-09-29T15:41:34+02:00: User store and data-layer conventions
**By:** Han (Backend Dev), requested by Leif Bjarte Johansson
**What:**
- Aspire connection names consumed by the API: `users` (Cosmos container, database `mammapuls`, partition key `/id`) and `media` (blob container). AppHost must expose exactly these names.
- User Id = `Base64Url(SHA256(UTF8(issuer + "|" + subject)))` — 43 chars, URL-safe, deterministic. Id is also the partition key. Helper: `CosmosUserStore.CreateId(issuer, subject)`.
- Pass `issuer` exactly as the token's `iss` claim value (no trimming/normalizing trailing slash) — changing it changes every user Id.
- Cosmos JSON uses System.Text.Json web defaults (camelCase): `id, issuer, subject, name, email, phoneNumber, createdAt, updatedAt`. No tokens are stored.
- `UpsertFromLoginAsync` is idempotent: patches name/email/phoneNumber/updatedAt for existing users (createdAt preserved), creates on first login. `DeleteAsync` is idempotent (404 ignored). `GetAsync` returns null on 404.
- Registration: `builder.AddUserStore()` (singleton `IUserStore`) and `builder.AddMediaStorage()` (only registers `BlobContainerClient`; no endpoints yet).
**Why:** Ackbar codes the login callback against `IUserStore`; Lando provisions the matching resources; R2 needs the Id derivation for tests.

### 2026-09-29T15:41:34+02:00: Vipps auth implementation (scaffolding)
**By:** Ackbar, requested by Leif Bjarte Johansson
**Status:** Implemented (uncommitted). Builds clean. A smoke test without Vipps credentials passed.

#### Scopes and stored data
- Scopes: `openid name email phoneNumber`. **Correction to the accepted decision:** the Vipps scope is `phoneNumber` (camelCase), not `phone_number`. The returned claims are `sub`, `name`, `email`, `phone_number` (source: Vipps Userinfo "Scopes" table and discovery `scopes_supported`, read 2026-09-29).
- Keep "ID token includes userinfo" **off** in the portal. Claims come from the userinfo endpoint (`GetClaimsFromUserInfoEndpoint = true`).
- Stored through `IUserStore.UpsertFromLoginAsync(issuer, sub, name, email, phone_number)`. The issuer comes from the discovery document, which the handler validated against the ID token `iss`.
- The upsert runs in `OnTicketReceived`, not `OnTokenValidated`, because the handler fetches userinfo only after `TokenValidated`.
- The cookie principal holds only `uid` (internal `AppUser.Id`) and `auth_time`. No Vipps tokens are kept (`SaveTokens = false`). Logs contain only the internal user id.

#### OIDC settings
- Code flow + PKCE (S256) and `response_mode=query`. PAR is explicitly disabled. `MapInboundClaims = false`. `ValidAlgorithms = RS256`.
- The audience is the handler default (= ClientId). **UNVERIFIED:** an older Vipps example shows `aud: ["vipps-integration"]`. Confirm with the first real test login.
- Token endpoint auth is **`client_secret_basic`** (the Vipps default). An `OnAuthorizationCodeReceived` hook redeems the code with `Authorization: Basic base64(client_id:client_secret)` and no id/secret in the body. It follows Vipps' documented encoding, which has no form-encoding step. Leave the portal setting at its default.
- The OIDC scheme is registered only when `Authority`, `ClientId` and `ClientSecret` are all set. Without them `GET /api/v1/auth/login` returns 503 and the rest of the API still works.
- Login errors (e.g. `access_denied`) redirect to the validated returnUrl with `?login=failed`.

#### Redirect URIs to register in the Vipps portal (must match exactly, no trailing slash)
- Test (local dev, https launch profile): `https://localhost:7176/signin-vipps`
- Prod (once the custom domain exists): `https://api.mammapuls.no/signin-vipps`
- ForwardedHeaders (`X-Forwarded-For`/`-Proto`, known proxies cleared for ACA ingress) makes the handler build an `https` redirect_uri behind Container Apps.

#### Cookie
- Name `__Host-mammapuls`: HttpOnly, Secure (always), SameSite=Lax, Path=/, no Domain (host-only on the API host).
- 1 h sliding expiry with a 12 h absolute cap (enforced through the `auth_time` claim in `OnValidatePrincipal`).
- For API calls the default challenge returns 401 and forbid returns 403, never a redirect. Only `/auth/login` challenges the Vipps scheme.
- Logout (`POST /api/v1/auth/logout`) signs out only the cookie scheme. Vipps has no end-session endpoint.

#### CSRF defence
- Every non-GET/HEAD/OPTIONS/TRACE request must carry a non-empty **`X-Requested-With`** header, or the API returns 400.
- Why this is enough:
  - SameSite=Lax already blocks cookies on cross-site POSTs.
  - Same-site attackers (e.g. another `*.mammapuls.no` subdomain) can't add a custom header without a CORS preflight, and the preflight only passes for origins in `Cors:AllowedOrigins`.
  - HTML forms can't set custom headers.
- **SPA contract:** send `credentials: 'include'` and `X-Requested-With: XMLHttpRequest` on every mutating request.
- **Future:** server-to-server webhooks (e.g. Vipps `CONSENT_REVOKED`) will need an explicit exemption.

#### CORS
- `Cors:AllowedOrigins` is an exact-origin allowlist with credentials.
- Allowed methods: GET/POST/PUT/PATCH/DELETE. Allowed headers: `Content-Type` and `X-Requested-With`. Preflight is cached for 10 min.
- Dev default: `http://localhost:5173`. Prod: empty until the SPA domain is known (set `Cors__AllowedOrigins__0=https://mammapuls.no`).

#### Open redirect
- `returnUrl` must be either:
  - a local path (`/…`, not `//` or `/\`, no control chars), or
  - an absolute http(s) URL, without userinfo, whose origin is in `Cors:AllowedOrigins`.
- Anything else returns 400. The default is the first allowed origin.

#### Authorization
- The fallback policy requires an authenticated user.
- Anonymous endpoints: `/api/v1/ping`, `/api/v1/auth/login`, `/api/v1/auth/logout`, `/openapi/v1.json`, `/health` and `/alive`. `.AllowAnonymous()` was added in ServiceDefaults.

#### DataProtection
- Application name `Mammapuls.Api`. Keys persist to `keys.xml` in the Aspire blob container connection `dataprotection`, through the keyed `BlobContainerClient` so it doesn't clash with Han's `media` client.
- Startup fails outside Development if `ConnectionStrings:dataprotection` is missing.
- In Development without it, keys stay local.
- Keys are not encrypted at rest beyond Storage SSE. Key Vault wrapping is deferred per the "no Key Vault for now" decision.

##### Single key store: blob, with ACA `autoConfigureDataProtection` disabled (2026-09-29T15:41:34+02:00)
- **Finding:** Aspire 13.5.4 emitted `configuration.runtime.dotnet.autoConfigureDataProtection: true` on `api`. This is a .NET feature, not Java. ACA supplies a platform-managed, read-only key directory through `ReadOnlyDataProtectionKeyDirectory`. ASP.NET Core `KeyManagementOptionsPostSetup` uses that directory only when the app has configured neither `XmlRepository` nor `XmlEncryptor`. Our `PersistKeysToAzureBlobStorage` sets `XmlRepository`, so the flag was a runtime no-op (it just logs "not using read-only key configuration"). It did make the infra ambiguous about which key store was used.
  - Sources: learn.microsoft.com/azure/container-apps/dotnet-overview ("Autoscaling considerations"); dotnet/aspnetcore `src/DataProtection/DataProtection/src/Internal/KeyManagementOptionsPostSetup.cs`; microsoft/aspire `ContainerAppContext.cs` (adds the flag for every `IDotnetProgramResource`, with no public opt-out API).
- **Change:** In `AppHost.cs`, `PublishAsAzureContainerApp` now calls `app.Configuration.ProvisionableProperties.Remove("AutoConfigureDataProtection")`. Customization callbacks run after Aspire adds the property. I verified with `aspire publish` that `api.bicep` has no `runtime` block and everything else is unchanged. The API version stays `2025-10-02-preview`.
- **Why blob over ACA-managed:**
  - We control the keys (we can revoke or rotate them).
  - They survive moving the app to another ACA environment or host.
  - Local dev behaves the same (Azurite).
  - The startup guard already makes blob mandatory outside Development.
- **At-rest protection and accepted risk:**
  - The container is private (`allowBlobPublicAccess: false`), shared-key auth is off (`allowSharedKeyAccess: false`), and access is Entra-only via the `api` managed identity. Storage SSE uses Microsoft-managed keys.
  - **Accepted risk:** `keys.xml` holds the key material in plaintext XML (no `ProtectKeysWith*`). Anyone with blob read on the account can forge or decrypt auth cookies. That includes the `api` identity and subscription Owners/Contributors who can grant themselves data-plane roles.
  - Key Vault wrapping (`ProtectKeysWithAzureKeyVault`) would require that attacker to also hold KV `unwrapKey`. It costs a Key Vault plus operational overhead, so it is deferred for cost.
  - **Revisit when:** more people or identities get Storage data-plane roles on this account, or the data becomes more sensitive than profile + session.
  - **Mitigations until then:** 12 h absolute cookie cap, and no Vipps tokens stored in the cookie.
  - **Incident response:** delete `keys.xml` and restart. This invalidates all sessions.
- **Watch:** Aspire may later add a first-class opt-out, or rename the property key. If `autoConfigureDataProtection` reappears in `aspire publish` output after an upgrade, re-apply.

#### Config keys
- `Authentication:Vipps:Authority`: test in appsettings.Development.json, prod in appsettings.json.
- `Authentication:Vipps:ClientId`: non-secret; env/user-secrets.
- `Authentication:Vipps:ClientSecret`: **secret**. Store it in user-secrets or an AppHost secret parameter, never in appsettings.
- `Cors:AllowedOrigins` (array).
- `ConnectionStrings:dataprotection`: provided by the AppHost.

#### Follow-ups
- **Lando:**
  - Add blob container `dataprotection` and reference it from `api`.
  - Pass the Vipps ClientId/ClientSecret as parameters (secret: true) mapped to `Authentication__Vipps__*`.
  - Ensure the API runs on the https endpoint 7176 locally.
- **R2:** tests for:
  - returnUrl validation
  - CSRF 400
  - 401 on `/me` without a cookie
  - CORS preflight allow/deny

### 2026-09-29T15:41:34+02:00: API test strategy (offline, in-process)
**By:** R2 (Tester), requested by Leif Bjarte Johansson
**What:**
- Test project: `tests/Mammapuls.Api.Tests` (in the `/tests/` folder of `Mammapuls.slnx`). It uses xUnit v3 4.0.1, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1 and Microsoft.AspNetCore.Mvc.Testing 10.0.12. All versions are in `Directory.Packages.props`.
- **Runner: VSTest, not MTP.** xUnit v3 4.x defaults to Microsoft.Testing.Platform, and on SDK 10 that breaks `dotnet test Mammapuls.slnx`. The csproj sets `UseMicrosoftTestingPlatformRunner=false` and `IsTestingPlatformApplication=false`, so the CI command works unchanged (`dotnet test Mammapuls.slnx --no-build -c Release`). Moving to MTP later means adding a `global.json` `test.runner` entry and changing CI to `dotnet test --solution Mammapuls.slnx`.
- `ApiFactory : WebApplicationFactory<Program>` runs the real pipeline with no network access:
  - Environment is `Development`.
  - `users` and `media` get dummy connection strings. The Aspire clients are created lazily and never resolved.
  - `IUserStore` is replaced with an in-memory store.
  - Data-protection keys are kept in memory.
  - The Vipps OIDC options get a static `OpenIdConnectConfiguration`, so the discovery document is never fetched. The challenge redirects to a fake authorize endpoint.
  - CORS allows `https://spa.mammapuls.test`.
- Auth in tests uses **real session cookies**, protected with the app's own `TicketDataFormat`. There is no fake auth scheme, so the cookie handler, `OnValidatePrincipal` (12 h cap), 401-not-redirect and sign-out are all exercised for real.
- **No production seams were needed.** .NET 10 generates the public `Program` class, so the API has no `public partial class Program;`.
- `CosmosUserStore.CreateId` is pinned to an independently computed test vector. Changing the derivation now fails a test, because it would orphan every stored user.
- **Deferred:** AppHost integration tests (`Aspire.Hosting.Testing`). They need Docker for the Cosmos and Azurite emulators, and CI has none today. They are also deferred for `CosmosUserStore` against the emulator (patch/create race, 404 handling).
**Why:** Fast, deterministic tests that CI can run without Azure, Docker or Vipps credentials, covering the auth, CSRF, CORS, open-redirect and cross-user boundaries.

### 2026-09-29T15:41:34+02:00: User directive — deploy on GitHub release
**By:** Leif Bjarte Johansson (via Copilot)
**What:** `aspire deploy` runs when a GitHub release is published (release-please creates releases with RELEASE_PLEASE_TOKEN, so the event fires). Manual `workflow_dispatch` stays as a fallback.
**Why:** User request

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
