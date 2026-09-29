# Project Context

- **Owner:** Leif Bjarte Johansson
- **Project:** Mammapuls REST API — backend for the Mammapuls website. Will hold personalized data, and later audio and video files. Very low budget.
- **Stack:** C# on latest .NET, .NET Aspire, ASP.NET Core minimal APIs, GitHub + GitHub Actions, Azure (cheapest tiers), Azure AI Foundry models, Vipps Login (OIDC)
- **Created:** 2026-09-29T15:23:39+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- 2026-09-29: Vipps Login access tokens are opaque and only work against userinfo, and there are no refresh tokens. The API must run the OIDC code flow itself (with `ResponseMode=query`, since `form_post` is unsupported) and issue its own cookie session. Key users on (issuer, sub), because sub differs per sales unit.
- 2026-09-29: Vipps scope names differ from the OIDC claim names: request `phoneNumber` and receive `phone_number`. In the ASP.NET OIDC handler, `OnTokenValidated` fires *before* userinfo, so profile-based user upsert belongs in `OnTicketReceived`. Disable PAR explicitly; otherwise the handler could send the secret in a form body. A `MapPost(path, handler(HttpContext))` binds to the RequestDelegate overload and discards typed results (ASP0016).
- 2026-09-29: For .NET projects, Aspire's ACA publisher always emits `runtime.dotnet.autoConfigureDataProtection: true`. ASP.NET Core ignores it when the app sets its own `XmlRepository`. We keep keys in blob only and strip the flag in `PublishAsAzureContainerApp` with `app.Configuration.ProvisionableProperties.Remove("AutoConfigureDataProtection")`. Also, `aspire publish` with parameter env vars saves those values to `~/.aspire/deployments/<hash>/production.json`, so delete that file after a dry run with dummy values.
- 2026-09-29: Environment-name gates aren't enough on their own. `appsettings.Development.json` ships in the publish output and container image by default, and AppHost forwards the deploy environment name. Keep dev-only secrets and fixtures out of publish (`CopyToPublishDirectory="Never"`), and also refuse dev-only features when `CONTAINER_APP_NAME` is set (ACA sets it on every app). `FixedTimeEquals` returns early on a length mismatch, so hash both sides first.

📌 Team update (2026-09-29T23:28:52+02:00): Temporary Development-only mock login `POST /api/v1/auth/mock-login` (issuer `urn:mammapuls:mock`, seeded leif/anne/kristoffer in appsettings.Development.json) shares `AuthExtensions.CreateSessionPrincipal` with the Vipps callback. `Authentication:MockLogin:Enabled` outside Development throws at startup — decided by Han (your security review is pending).

📌 Team update (2026-09-29T23:28:52+02:00): Mock login removed before commit because the Vipps keys arrived; your review is merged but marked SUPERSEDED. Only your csproj rule excluding appsettings.Development.json from publish survives — decided by Han. Open for review: `aud` validation and the cross-site cookie on `*.azurecontainerapps.io` (see "Vipps credential locations").
