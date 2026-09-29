# Project Context

- **Owner:** Leif Bjarte Johansson
- **Project:** Mammapuls REST API — backend for the Mammapuls website. Will hold personalized data, and later audio and video files. Very low budget.
- **Stack:** C# on latest .NET, .NET Aspire, ASP.NET Core minimal APIs, GitHub + GitHub Actions, Azure (cheapest tiers), Azure AI Foundry models, Vipps Login (OIDC)
- **Created:** 2026-09-29T15:23:39+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-09-29T15:23:39+02:00): Aspire scaffold exists (Mammapuls.slnx, net10.0, Aspire 13.5.4; tests go under tests/); Azure architecture (Leia) and Vipps Login (Ackbar) proposals pending user approval — decided by Han

- 2026-09-29T15:41:34+02:00 — tests/Mammapuls.Api.Tests (43 tests, all pass offline).
  - xUnit v3 4.x defaults to MTP, and on SDK 10 that fails `dotnet test <sln>` (VSTest target error). Set `UseMicrosoftTestingPlatformRunner=false` and `IsTestingPlatformApplication=false` in the csproj.
  - xUnit v3 analyzers need `TestContext.Current.CancellationToken` passed everywhere (TreatWarningsAsErrors).
  - Authenticate by protecting a real ticket with the app's cookie `TicketDataFormat`. Send it as a raw `Cookie` header with `HandleCookies=false`; otherwise the client never sends the `__Host-`/Secure cookie over http://localhost.
  - Stop OIDC discovery by calling `Configure<OpenIdConnectOptions>("Vipps", o => o.Configuration = ...)`. This must be Configure, not PostConfigure: the handler's post-configure builds the ConfigurationManager first.
  - `UseSetting` overrides appsettings.Development.json and is visible to Program's early config reads.
  - The run_in_terminal sync mode lost dotnet output here; async mode + kill worked.
