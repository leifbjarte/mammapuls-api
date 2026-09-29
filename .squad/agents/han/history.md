# Project Context

- **Owner:** Leif Bjarte Johansson
- **Project:** Mammapuls REST API — backend for the Mammapuls website. Will hold personalized data, and later audio and video files. Very low budget.
- **Stack:** C# on latest .NET, .NET Aspire, ASP.NET Core minimal APIs, GitHub + GitHub Actions, Azure (cheapest tiers), Azure AI Foundry models, Vipps Login (OIDC)
- **Created:** 2026-09-29T15:23:39+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-09-29T15:23:39+02:00: Initial scaffold
- Toolchain: .NET SDK 10.0.301 (latest installed; no .NET 11 SDK present) → `net10.0`. Aspire CLI 13.5.4; `Aspire.AppHost.Sdk/13.5.4` with `AspireUseCliBundle=true` (otherwise ASPIRE010 warning).
- Installed `dotnet new` Aspire templates were 13.3.4 (pulled vulnerable MessagePack) — generated from them, then bumped SDK/packages. `aspire new aspire-empty` produces a single-file `apphost.cs`, not a project.
- Layout: `Mammapuls.slnx` at root; `src/Mammapuls.AppHost`, `src/Mammapuls.ServiceDefaults`, `src/Mammapuls.Api`. `aspire.config.json` at repo root points to the AppHost csproj.
- `Directory.Build.props` owns TFM/Nullable/ImplicitUsings/TreatWarningsAsErrors (NU1901-1904 kept as warnings). CPM via `Directory.Packages.props` works with the Aspire AppHost SDK.
- API: endpoints in `Endpoints/*Endpoints.cs` as `RouteGroupBuilder` extensions, mounted under `/api/v1` in Program.cs. AppHost resource name `api`, health check `/health` (Development only per ServiceDefaults).
- `dotnet package list --project <slnx> --outdated` is the .NET 10 form of `dotnet list package`.

### 2026-09-29T15:41:34+02:00: Data layer scaffold
- Aspire client packages `Aspire.Microsoft.Azure.Cosmos` / `Aspire.Azure.Storage.Blobs` 13.5.4 (Cosmos SDK 3.61, Blobs 12.28; Newtonsoft 13.0.4 comes transitively, so the Cosmos Newtonsoft build check passes).
- `builder.AddAzureCosmosContainer("users")` registers a singleton `Container` (db/container come from the connection string); `builder.AddAzureBlobContainerClient("media")` registers `BlobContainerClient`.
- Cosmos serializer set to STJ web defaults via `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions` → camelCase, `Id` → `id`; patch paths must be camelCase.
- User store: `Users/` folder, `CosmosUserStore` singleton; upsert = Patch (keeps CreatedAt) → Create on 404 → retry Patch on 409.
- Running `dotnet build` while other agents build concurrently can fail silently (exit 1, no output) — re-run.
- 2026-09-29T15:41:34+02:00: Hashing composite keys needs an injective encoding — `CreateId` now hashes `{issuer.Length}:{issuer}{subject.Length}:{subject}` (a bare `|` separator let ("a|b","c") collide with ("a","b|c")).
- 2026-09-29T22:27:01+02:00: Aspire publish does NOT pass the deploy environment to project containers (they default to Production) — AppHost sets `ASPNETCORE_ENVIRONMENT` in publish mode; Scalar at `/scalar/v1` (Dev/Test) gets the CSRF header via a required OpenAPI header param with default, and unmatched routes return 401 (not 404) to anonymous callers because of the fallback policy.
- 2026-09-29T23:28:52+02:00: Dev-only mock login (`POST /api/v1/auth/mock-login`, `MockLoginOptions`, `MockUserSeeder`, issuer `urn:mammapuls:mock`). Gating is: options are only `Configure`d when enabled in Development, so `IOptions<MockLoginOptions>.Value.Enabled` doubles as the "active" flag at map time. "Route not mapped" means anonymous → 401, signed-in → 404 (R2's test asserts this; don't add a 404 stub). Cookie is `Secure`/`__Host-`, so manual testing must use HTTPS.
- 2026-09-29T23:28:52+02:00: Mock login removed uncommitted once the Vipps keys arrived. The rate limiter existed only for it, so it went too. When ripping out a temporary feature, also remove the infrastructure it pulled in, then check `git diff` against HEAD. Here AuthExtensions/AuthConstants/AuthEndpoints ended up byte-identical to HEAD.
