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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
