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
