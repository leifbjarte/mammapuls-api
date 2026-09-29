# Project Context

- **Owner:** Leif Bjarte Johansson
- **Project:** Mammapuls REST API — backend for the Mammapuls website. Will hold personalized data, and later audio and video files. Very low budget.
- **Stack:** C# on latest .NET, .NET Aspire, ASP.NET Core minimal APIs, GitHub + GitHub Actions, Azure (cheapest tiers), Azure AI Foundry models, Vipps Login (OIDC)
- **Created:** 2026-09-29T15:23:39+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- 2026-09-29: Aspire's Azure defaults are not cheapest. App Service env defaults to P0v3 (~$65/mo), Cosmos to serverless (not free-tier compatible), SQL to the free offer with AutoPause, and Log Analytics has no daily cap. Always override via ConfigureInfrastructure. Sweden Central is the EU region with the broadest Foundry Data Zone coverage.

📌 Team update (2026-09-29T15:41:34+02:00): Azure architecture accepted (with Fact Checker corrections) and implemented in AppHost by Lando; open: budget alert + per-meter cost check after first deploy — decided by Leif Bjarte Johansson
