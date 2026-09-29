# Project Context

- **Owner:** Leif Bjarte Johansson
- **Project:** Mammapuls REST API — backend for the Mammapuls website. Will hold personalized data, and later audio and video files. Very low budget.
- **Stack:** C# on latest .NET, .NET Aspire, ASP.NET Core minimal APIs, GitHub + GitHub Actions, Azure (cheapest tiers), Azure AI Foundry models, Vipps Login (OIDC)
- **Created:** 2026-09-29T15:23:39+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-09-29T15:23:39+02:00): Aspire scaffold exists (Mammapuls.slnx, net10.0, Aspire 13.5.4; HTTPS redirection omitted pending your ingress TLS confirmation); Azure architecture (Leia) and Vipps Login (Ackbar) proposals pending user approval — decided by Han

- 2026-09-29T15:41:34+02:00: Azure model in AppHost (Aspire 13.5.4). Property names that differ from Bicep: Cosmos free tier is `CosmosDBAccount.IsFreeTierEnabled`; clearing `Capabilities` drops `EnableServerless`. LAW cap via `OperationalInsightsWorkspace.WorkspaceCapping.DailyQuotaInGB`. ACA app is `app.Template.Scale` / `app.Template.Containers.Single().Value!.Resources` (no `.Value` on Template). `aspire publish -o $env:TEMP\...` works offline and is the dry-run check. Aspire's main.bicep is subscription-scoped (creates RG), so the deploy principal needs subscription-level rights. ACA app sets `runtime.dotnet.autoConfigureDataProtection: true` by default.
- 2026-09-29T15:41:34+02:00: Release Please = `googleapis/release-please-action@v5` (v5.0.0, node24; README examples still say v4). Manifest mode is the default when `release-type` input is omitted. `simple` strategy's version.txt is `createIfMissing: false`, so none needed. `.props` isn't an auto-typed extension — register it as `{ "type": "generic", "path": "Directory.Build.props" }` with an inline `<!-- x-release-please-version -->` marker. Schema default `include-component-in-tag` is true → set false for plain `vX.Y.Z` tags. Hidden-only commits (chore/ci/docs) don't open a release PR. GITHUB_TOKEN-created PRs don't trigger ci.yml; workflow uses `secrets.RELEASE_PLEASE_TOKEN || github.token`.
