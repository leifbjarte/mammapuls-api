---
updated_at: 2026-09-29T23:28:52+02:00
focus_area: Wire the real Vipps test credentials and deploy to Test
active_issues: []
---

# What We're Focused On

The Vipps test client id and secret have arrived and the mock login has been removed, so Vipps Login is the only sign-in path. Next: set the AppHost user-secrets and the GitHub `test` environment values (names are in decisions.md, "Vipps credential locations"), register the redirect URIs, set a real `SPA_ORIGIN`, and redeploy to Test. Open checks: move repo-level Vipps values to env scope, the cross-site cookie on `*.azurecontainerapps.io`, and `aud` validation.
