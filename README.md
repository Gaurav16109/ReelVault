# ReelVault

## Solution layout

- `ReelVault.Api` — ASP.NET Core Web API (.NET 10), EF Core + Npgsql
  - `GET /api/health` — Phase 0 health check (writes/reads a row in Postgres)
  - `POST /api/extract/food` — Phase 1: turns a Reel caption into structured food data via Gemini
- `ReelVault.App` — .NET MAUI app (.NET 10), Mac Catalyst only — a single extraction screen
- `ReelVault.Shared` — DTOs shared between API and App (`HealthCheck`, `HealthResponse`, `FoodExtraction`, `ExtractionRequest`, `ExtractionResponse`)
- `docker-compose.yml` — local PostgreSQL

## One-time tool setup

Check what you already have first:

```bash
dotnet --version           # expect 10.0.201 or newer
dotnet workload list       # expect maui-maccatalyst
```

1. **.NET 10 SDK** — install from https://dotnet.microsoft.com/download/dotnet/10.0 if `dotnet --version` doesn't show it.

2. **MAUI workload** (Mac Catalyst only — no Android/iOS/Windows):

   ```bash
   dotnet workload install maui-maccatalyst
   ```

3. **Full Xcode** (not just Command Line Tools) — Mac Catalyst builds require it even to compile, not only to run. Install from the App Store, then:

   ```bash
   sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
   sudo xcodebuild -license accept
   ```

   Verify with `xcode-select -p` — it must point inside `/Applications/Xcode.app`, not `/Library/Developer/CommandLineTools`.

   > If your installed Xcode is newer than what the `maui-maccatalyst` workload pack expects, you'll hit `This version of .NET for MacCatalyst requires Xcode X.Y`. `ReelVault.App.csproj` already sets `<ValidateXcodeVersion>false</ValidateXcodeVersion>` to skip that strict check — remove it once Microsoft ships a matching workload pack.

4. **EF Core CLI tool** (used to apply/generate migrations):

   ```bash
   dotnet tool install -g dotnet-ef
   ```

5. **Docker Desktop** (for local Postgres) — https://www.docker.com/products/docker-desktop, then make sure it's running (`docker info` should not error).

6. **Gemini API key** (Phase 1 only) — get a free key at https://aistudio.google.com/apikey, then store it in .NET user-secrets (never in `appsettings.json`, never committed):

   ```bash
   cd ReelVault.Api
   dotnet user-secrets set "Gemini:ApiKey" "<your-key>"
   cd ..
   ```

## Running everything

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

## Regenerating the migration

If you change `ReelVaultDbContext` or its entities later:

```bash
cd ReelVault.Api
dotnet ef migrations add <Name>
dotnet ef database update
```

## Phase 1: Food caption extraction

Paste a Reel caption into the app (or `curl` the endpoint directly) and it comes back as structured JSON — nothing is saved to Postgres in this phase, extraction only.

```bash
curl -X POST http://localhost:5235/api/extract/food \
  -H "Content-Type: application/json" \
  -d '{"sourceUrl": "", "captionText": "best pizza ever go check it out"}'
```

The API calls Google Gemini (model configurable via `Gemini:Model` in `appsettings.json`, defaults to `gemini-flash-latest`) with a prompt that forbids guessing: any field not explicitly stated in the caption comes back `null`, and `NotMentionedFields` in the response lists exactly which ones. `RawModelOutput` carries the model's raw JSON text so you can judge extraction quality directly.

Errors are handled without crashing: an empty caption is a `400`, a Gemini transport/API failure is a `502`, and a response that fails to parse as the expected schema is a `422` (with `RawModelOutput` still attached so you can see what the model actually said).

### Phase 1 test captions

Paste each of these into the app's caption box (or the `curl` body above) to sanity-check extraction quality across the range from detailed to sparse to messy.

**1. Detailed — most fields should be filled**
```
Found the BEST butter chicken in Koramangala! 🍗🔥 Spice Route, 5th Block, Bangalore.
Rated 4.6⭐ on Zomato. Must try their Butter Chicken, Garlic Naan, and Dal Makhani.
Price for two: ₹800-1000. Open 12pm-11pm daily. They have a solid veg menu too,
dedicated veg kitchen. Parking available on the street outside. Go for the ambience
and the crazy paneer tikka!
```
Expect: Name, Area, City, PriceRange, MustTry, Rating, OpeningHours, VegOptions, Parking, MapQuery, and Summary all filled. `Cuisine` should correctly come back `null` — the caption never says "North Indian" or "Indian" in words, and the model must not infer it just because butter chicken implies it.

**2. Sparse — most fields should be `null`**
```
best pizza ever 😍 go check it out
```
Expect: only `MustTry` (pizza) and maybe `Cuisine` (pizza) filled; `Name`, `Area`, `City`, `PriceRange`, `Rating`, `OpeningHours`, `MapQuery`, `VegOptions`, `Parking` all `null` and listed in `NotMentionedFields`. `MapQuery` must be `null` too, since no name/area/city is known to compose it from.

**3. Messy / emoji-heavy — tests robustness, not just sparseness**
```
OMGGG 😭😭🔥🔥 u guys NEED to try this place fr fr no cap 🍜🥢💯 @noodle.house.blr
📍somewhere near indiranagar i think?? open till late, super cheap, like 300 bucks
for 2 was PLENTYYY 🤑 ramen was 🔥🔥🔥 no veg options tho unfortunately for my
veggie friends 🥲 #foodie #bangalorefoodie #ramen #musttry
```
Verified output with `gemini-flash-latest`: `Name` = "noodle.house.blr" (from the handle), `Area` = "Indiranagar" (accepted despite the caption's own "i think??" hedge), `City` = "Bangalore" (from the `#bangalorefoodie` hashtag), `Cuisine` = "Ramen", `PriceRange` = "300 bucks for 2", `MustTry` = ["ramen"], `OpeningHours` = "open till late", `VegOptions` = "no veg options" — and correctly `Rating` = `null` and `Parking` = `null`, since neither is mentioned anywhere in the text.

This third caption is deliberately ambiguous — the point isn't one single "correct" output (a handle-as-name or a hashtag-as-city call are both defensible), it's to check the model **never invents a rating, exact address, or parking info** that plain isn't in the text, and never crashes on emoji/hashtag-heavy input.

### Phase 1 checklist

- [ ] `Gemini:ApiKey` is set via `dotnet user-secrets set` (confirm with `dotnet user-secrets list` in `ReelVault.Api` — never check this key into git or print it in logs)
- [ ] `dotnet build` succeeds for `ReelVault.Api`, `ReelVault.Shared`, and `ReelVault.App` (`-f net10.0-maccatalyst`)
- [ ] `POST /api/extract/food` with caption 1 (detailed) returns populated fields and a correctly-`null` `Cuisine`
- [ ] `POST /api/extract/food` with caption 2 (sparse) returns mostly-`null` fields, `null` `MapQuery`, and `NotMentionedFields` lists them
- [ ] `POST /api/extract/food` with caption 3 (messy) returns a result without crashing, and does not fabricate a `Rating`, `OpeningHours`, or exact `Parking` value that isn't in the text
- [ ] `POST /api/extract/food` with `captionText: ""` returns `400`
- [ ] Temporarily breaking `Gemini:ApiKey` (e.g. append garbage) returns `502` with a clear `detail`, not a 500 crash — restore the key afterward
- [ ] The MAUI app launches on Mac Catalyst, and using the on-screen form (URL optional, caption required, tap Extract) shows the same structured result as the `curl` call, with "— not mentioned" for null fields
- [ ] The app shows a visible loading state while extracting and a visible error message if the API is stopped

**Pass criterion:** all boxes checked, and for all three sample captions, every field that is `null` in the response corresponds to information genuinely absent from the caption — not a fabricated guess. If any field appears that isn't actually stated in the caption (an invented rating, price, or opening hours), extraction quality is failing regardless of how "complete" the result looks.
