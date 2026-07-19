# ReelVault — Phase 0 (Foundation)

Phase 0 proves the wiring: the MAUI app can call the ASP.NET API, and the API can reach PostgreSQL. No features yet.

## Solution layout

- `ReelVault.Api` — ASP.NET Core Web API (.NET 10), EF Core + Npgsql, one endpoint: `GET /api/health`
- `ReelVault.App` — .NET MAUI app (.NET 10), Mac Catalyst only, one page with a "Test API Connection" button
- `ReelVault.Shared` — DTOs/models shared between API and App (`HealthCheck`, `HealthResponse`)
- `docker-compose.yml` — local PostgreSQL

## One-time tool setup

Check what you already have first:

```bash
dotnet --version           # expect 10.0.201 or newer
dotnet workload list       # expect maui-maccatalyst
```

1. **.NET 10 SDK** — install from https://dotnet.microsoft.com/download/dotnet/10.0 if `dotnet --version` doesn't show it.

2. **MAUI workload** (Mac Catalyst only for Phase 0 — no Android/iOS/Windows):

   ```bash
   dotnet workload install maui-maccatalyst
   ```

3. **Full Xcode** (not just Command Line Tools) — Mac Catalyst builds require it even to compile, not only to run. Install from the App Store, then:

   ```bash
   sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
   sudo xcodebuild -license accept
   ```

   Verify with `xcode-select -p` — it must point inside `/Applications/Xcode.app`, not `/Library/Developer/CommandLineTools`.

4. **EF Core CLI tool** (used to apply/generate migrations):

   ```bash
   dotnet tool install -g dotnet-ef
   ```

5. **Docker Desktop** (for local Postgres) — https://www.docker.com/products/docker-desktop, then make sure it's running (`docker info` should not error).

## Running Phase 0

Run these in order from the repository root.

```bash
# 1. Start Postgres
docker compose up -d

# 2. Apply the EF Core migration (creates the reelvault database schema)
cd ReelVault.Api
dotnet ef database update
cd ..

# 3. Run the API (listens on http://localhost:5235)
cd ReelVault.Api
dotnet run
# leave this running in its own terminal
```

In a second terminal, confirm the API works on its own:

```bash
curl http://localhost:5235/api/health
# {"status":"ok","dbConnected":true,"totalChecks":1}
```

Then run the MAUI app on Mac Catalyst:

```bash
cd ReelVault.App
dotnet build -t:Run -f net10.0-maccatalyst
```

Tap **Test API Connection** — it should show the same JSON the `curl` call returned.

## Regenerating the migration

If you change `ReelVaultDbContext` or its entities later:

```bash
cd ReelVault.Api
dotnet ef migrations add <Name>
dotnet ef database update
```

## Phase 0 completion checklist

- [x] `docker compose up -d` starts Postgres with no errors
- [x] `dotnet ef database update` applies `InitialCreate` without errors
- [x] `dotnet run` in `ReelVault.Api` starts and logs `Now listening on: http://localhost:5235`
- [x] `curl http://localhost:5235/api/health` returns `{"status":"ok","dbConnected":true,"totalChecks":N}` with `N` incrementing on repeated calls
- [ ] Full Xcode installed and selected (`xcode-select -p` points inside `/Applications/Xcode.app`)
- [ ] `dotnet build -f net10.0-maccatalyst` succeeds in `ReelVault.App`
- [ ] The app launches and tapping "Test API Connection" shows the same JSON as the `curl` call (not an error)

Once every box is checked, the foundation is solid and later phases (categories, LLM parsing, search, share sheet) can build on top of it.
