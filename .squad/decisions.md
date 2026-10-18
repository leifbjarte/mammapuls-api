# Squad Decisions

## Active Decisions

> Entries older than 7 days are in `decisions-archive.md`.

### 2026-10-18T18:18:41+02:00: GitHub deploy uses user-assigned managed identity in rg-common

- **Date:** 2026-10-18 · **By:** Lando (requested by Leif Bjarte Johansson)
- **What:** GitHub Actions deploy now authenticates (OIDC) as UAMI `id-github-mammapuls-api` in `rg-common` (norwayeast) instead of Entra app reg `1f166d44-1e5b-4042-9152-9fa1d99f0f48`. Follows the `id-github-<repo>` convention used by sibling repos.
  - clientId `65147293-69e2-46f7-bc05-e62d653a31e0`, principalId `085693d8-0cdd-4c9e-bae4-68d33eb8d2d0`
  - Federated creds: `github-mammapuls-api-test` (`repo:leifbjarte/mammapuls-api:environment:test`), `github-mammapuls-api-prod` (`...:environment:production`)
  - Roles mirror the app reg exactly (subscription scope): Contributor + Role Based Access Control Administrator with the standard condition blocking assignment of Owner / UAA / RBAC Admin.
  - Repo variable `AZURE_CLIENT_ID` updated; no env-level overrides. `deploy.yml` unchanged (azure/login inputs are identical).
- **Why:** No app-registration lifecycle to manage; identity is an Azure resource managed via ARM (Graph is blocked by Conditional Access on this device).
- **Cost:** UAMI and federated credentials are free.
- **Follow-up:** After a successful test + production deploy, remove app reg `1f166d44-…` and its two subscription role assignments (SP object id `c38930d8-e18f-4e45-a0e4-37a44bba6a67`). Consider narrowing scope from subscription to the mammapuls RGs later.

### 2026-10-18T18:17:10+02:00: Aspire deploys use the shared ACR `leifssharedacr`
**By:** Lando (DevOps), requested by Leif Bjarte Johansson
**What:**
- AppHost references the existing registry: `builder.AddAzureContainerRegistry("acr").PublishAsExisting(acrName, acrResourceGroup)` and `AddAzureContainerAppEnvironment("aca").WithAzureContainerRegistry(acr)`.
- Parameters `acr-name` (default `leifssharedacr`) and `acr-resource-group` (default `rg-common`) are published as Bicep defaults; override with `Parameters__acr_name` / `Parameters__acr_resource_group`.
- The registry is Basic SKU in `norwayeast`; ACA stays in `swedencentral` (cross-region pull, negligible egress).
- The API image is pushed as `mammapuls/api` (`WithRemoteImageName`, experimental ASPIREPIPELINES003) so it can't collide with other projects' `api` repositories.
- Generated Bicep: the registry is `existing` only; `aca-mi-roles-acr` assigns AcrPull to the ACA environment's managed identity at the registry scope (deployed into `rg-common`).
**Required RBAC for the GitHub deploy identity (manual, not granted by Lando):**
- `AcrPush` on `leifssharedacr` (image push).
- Role-assignment write on `leifssharedacr` (e.g. Role Based Access Control Administrator, constrained to AcrPull) for the AcrPull grant.
- `Microsoft.Resources/deployments/write` in `rg-common` (nested Bicep modules run there).
**Why:** Very low budget; one shared Basic registry (~$5/month) instead of one per environment.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
