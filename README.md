# ReelVault

## Solution layout

- `ReelVault.Api` — ASP.NET Core Web API (.NET 10), EF Core + Npgsql
  - `GET /api/health` — Phase 0 health check (writes/reads a row in Postgres)
  - `POST /api/extract/food`, `POST /api/extract/travel` — turn a Reel caption into structured data via Gemini (Phase 1 Food, Phase 3 Travel)
  - `POST/GET/PUT/DELETE /api/items`, plus `/search`, `/categories`, `/locations` — save, browse, search, filter, edit, and archive extracted items across all categories (see Phase 2/3 below)
- `ReelVault.App` — .NET MAUI app (.NET 10), Mac Catalyst + Android (Phase 4a) — Home (category tiles) → Extract/List → Detail/Edit screens
- `ReelVault.Shared` — DTOs shared between API and App (`FoodExtraction`, `TravelExtraction`, `ExtractionRequest`, `ExtractionResponse`/`TravelExtractionResponse`, `SavedItemListDto`, `SavedItemDetailDto`, `SaveItemRequest`/`Result`, `UpdateItemRequest`, `ItemStatus`, `CategoryCount`, `LocationsResult`, `InstagramUrlValidator`, ...)
- `ReelVault.Api.Tests` — xUnit tests (extraction parsing for both categories, dedupe matching, JSONB round-trip, soft-delete, update logic, URL validation, category/location filters — no live network/Gemini/Postgres required)
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

## Phase 2: Save, browse, edit, dedupe

Extracted food data can now be saved to Postgres, browsed in a list, and edited. Single-user for now (no auth) — `SavedItem.UserId` exists and is left `null`, reserved for a future multi-user migration.

### Schema

`SavedItems` has one set of core columns shared by every future category (Food, Travel, ...) plus a single `CategoryData jsonb` column holding whatever fields are specific to that category (`FoodExtraction` for `Category = "Food"`). Adding a new category later means adding a new nullable `*Data` DTO field and a new JSON shape — not a new migration. Rows are **never hard-deleted**: `DELETE /api/items/{id}` sets `IsDeleted = true` and `Status = Archived`; the row stays in the table forever.

### Endpoints

- `POST /api/items` — save. Runs dedupe first (see below); on success returns `201` with the saved item, on a likely duplicate returns `409` with `{ possibleDuplicate: true, existingItemId, existingItemTitle }` and does **not** save. Pass `forceSave: true` to save anyway despite a duplicate.
- `GET /api/items` — lightweight list (`id`, `title`, `category`, `summary`, `thumbnailUrl`, `status`, `area`, `city`, `savedAt`), newest first, excludes soft-deleted items.
- `GET /api/items/{id}` — full detail including `foodData` (deserialized from `CategoryData`). Still returns soft-deleted items (so a deep link doesn't 404), just marked `isDeleted: true`.
- `PUT /api/items/{id}` — full-replace edit of `title`, `summary`, `userNotes`, `status`, and `foodData`; updates `updatedAt` (never `savedAt`).
- `DELETE /api/items/{id}` — soft delete as described above. Returns `204`.

### Dedupe

Before saving, `DuplicateDetector.FindPossibleDuplicate` (in `ReelVault.Api/Items/DuplicateDetector.cs` — pure, no DB dependency, takes a candidate list) checks, scoped to the same `Category`: an exact (normalized) `SourceUrl` match, OR the same normalized `Title` + `Area`. A match **warns**, it never silently blocks or overwrites — the caller decides via `forceSave`.

### Thumbnails — current limitation

`IThumbnailFetcher` / `OEmbedThumbnailFetcher` (in `ReelVault.Api/Thumbnails/`) attempt Instagram's official oEmbed endpoint, gated behind a Facebook Graph API access token (`Instagram:OEmbedAccessToken` in config/user-secrets). **No token is configured in this project**, so thumbnail fetching currently always returns `null` — every saved item's `ThumbnailUrl` is `null`, and the app renders its first-letter placeholder tile instead. This is intentional: Instagram's oEmbed requires an approved Facebook Developer app, and scraping the reel page's HTML for Open Graph tags would go against this project's no-scraping stance. A save **never fails** because of this — `IThumbnailFetcher` is isolated behind an interface specifically so this can be swapped for a working implementation (or a configured token) later without touching the rest of Phase 2.

### Running Phase 2

Same setup as above (`docker compose up -d`, `dotnet ef database update` picks up the new `AddSavedItems` migration, `dotnet run`), then either use the app's Save/Saved Items/Detail screens, or `curl` directly:

```bash
# Save
curl -X POST http://localhost:5235/api/items -H "Content-Type: application/json" -d '{
  "category": "Food", "sourceUrl": "https://instagram.com/reel/abc123",
  "title": "Spice Route", "foodData": { "name": "Spice Route", "area": "Koramangala", "city": "Bangalore" }
}'

# List / detail / edit / archive
curl http://localhost:5235/api/items
curl http://localhost:5235/api/items/<id>
curl -X PUT http://localhost:5235/api/items/<id> -H "Content-Type: application/json" -d '{"title":"...", "status":"Visited", "foodData": {...}}'
curl -X DELETE http://localhost:5235/api/items/<id>

# Duplicate signal (same sourceUrl or same normalized Title+Area)
curl -X POST http://localhost:5235/api/items -H "Content-Type: application/json" -d '{"category":"Food","sourceUrl":"https://instagram.com/reel/abc123","title":"Spice Route"}'
# -> 409 { "possibleDuplicate": true, "existingItemId": "...", "existingItemTitle": "Spice Route" }
```

### Phase 2 checklist

- [ ] `dotnet ef database update` applies `AddSavedItems`; `\d "SavedItems"` in psql shows the table with a `jsonb` `CategoryData` column
- [ ] `dotnet test` passes — Phase 1's parsing tests plus Phase 2's dedupe/serialization/repository tests
- [ ] POST creates an item; GET list shows it; GET detail shows full `foodData`; PUT changes the intended fields and `updatedAt` (not `savedAt`); DELETE removes it from the list while the row still exists in Postgres
- [ ] POSTing a duplicate (same `sourceUrl`, or same `Title`+`Area`) returns `409` with `possibleDuplicate: true` instead of saving
- [ ] `forceSave: true` saves a duplicate anyway as a new row, without touching the existing one
- [ ] The MAUI app builds for `net10.0-maccatalyst`; Extract → Save → Saved Items → tap a row → Detail/Edit → Archive all navigate correctly
- [ ] Searching the repo for your Gemini key's actual prefix (e.g. `grep -rln "AQ" . --exclude-dir=.git`, excluding this checklist line itself) turns up nothing — the key is never hardcoded, logged, or committed

**Pass criterion:** all boxes checked, soft-delete never removes a row, and the duplicate signal never silently blocks or silently overwrites — the user always gets Update/Save anyway/Cancel.

## Phase 3: Travel category, full-text search, filters, URL validation

Travel is a second category that reuses every piece of Phase 2's persistence (same `SavedItems` table, same dedupe, same soft-delete, same CRUD) — this phase exists specifically to prove the JSONB-per-category design scales without a schema change per category.

### Travel category

`TravelExtraction` (`PlaceName`, `Area`, `City`, `PlaceType`, `BestTimeToVisit`, `EstimatedCost`, `Highlights`, `Activities`, `MapQuery`, `NearbyPlaces`, `Summary`) follows the exact same rules as `FoodExtraction`: only what's explicitly in the caption, `null` for everything else, `MapQuery` is the one field the model composes from the others. `POST /api/extract/travel` mirrors `/food`. Saving passes `travelData` instead of `foodData` in `SaveItemRequest`/`UpdateItemRequest` — the API dispatches on whichever payload is actually present (`CategoryDataSerializer.Serialize`/`ResolveAreaCity`), not by trusting the `category` string, so a mismatched category can't silently corrupt data.

`GeminiFoodExtractor` was renamed `GeminiExtractor` and refactored to share one HTTP-calling core between `ExtractFoodAsync`/`ExtractTravelAsync` (`ILlmExtractor.LlmExtractionResult` is now generic: `LlmExtractionResult<T>`) — Food's prompt, parsing, and error handling are byte-for-byte unchanged, just no longer duplicated for Travel.

### Area/City promoted to real columns

`SavedItem.Area`/`City` are now indexed `text` columns (previously only inside `CategoryData` JSONB), populated from whichever category payload was saved. The `AddLocationAndSearch` migration backfills them for pre-existing rows from `CategoryData->>'Area'`/`'City'` before adding anything that depends on them. Everything category-specific still lives only in JSONB — these two columns are promoted because every category has them and filtering/sorting on real columns is what makes `/api/items/locations` and the city/area filters below fast.

### Full-text search

`SavedItem.SearchVector` is a Postgres `GENERATED ALWAYS AS (...) STORED` `tsvector` column (see `ReelVaultDbContext.OnModelCreating` — only configured `if (Database.IsNpgsql())`, since the EF InMemory provider used by this project's own tests can't map `NpgsqlTsVector` at all) over `Title`, `Summary`, `Area`, `City`, and the category-specific `Name`/`PlaceName`/`Cuisine`/`PlaceType`/`MustTry`/`Highlights` fields pulled out of `CategoryData` with `->>`. A GIN index (`.HasMethod("gin")`) makes it fast. `GET /api/items/search?q=...` (and plain `GET /api/items?q=...` — same underlying query) ranks by `ts_rank` via `EF.Functions.PlainToTsQuery(...)`/`.Matches()`/`.Rank()`, and works across categories in one query — no per-category branching.

One EF Core gotcha hit and fixed along the way: `EF.Functions.PlainToTsQuery(...)` must be called *inline inside* the `.Where()`/`.OrderByDescending()` lambdas, not evaluated into a local variable first — assigning it to a variable makes EF actually try to invoke the marker method at runtime instead of translating it to SQL, which throws.

### Filters + category/location endpoints

`GET /api/items` and `/search` both accept optional, combinable `q`, `category`, `city`, `area` query params — any subset, e.g. `?category=Travel&city=Goa&q=beach`. City/area matching is case-insensitive via `.ToLower()` (not `EF.Functions.ILike`, which is Postgres-only and doesn't translate on the InMemory provider this project's tests use).

- `GET /api/items/categories` → `[{ "category": "Food", "count": 12 }, ...]` — drives the app's category home screen. A category only appears once it has at least one saved item; there's no hardcoded list anywhere.
- `GET /api/items/locations?category=...` → `{ "cities": [...], "areas": [...] }` — distinct values, optionally scoped to a category, for the app's filter dropdowns.

### Instagram URL validation

`InstagramUrlValidator` (`ReelVault.Shared` — pure, no network, shared by API and app so the app gets instant feedback while typing) checks the host is `instagram.com`/`*.instagram.com` and the path looks like `/reel/`, `/reels/`, `/p/`, or `/tv/` followed by a shortcode, and normalizes to `https://www.instagram.com/{type}/{shortcode}/` (canonical host/scheme, tracking query params like `?igshid=...` stripped). It's a **warning, never a block** — the Extract screen shows an orange note under the URL field if it doesn't look right, but caption-only saves (no URL, or an unrecognized URL) are always allowed.

### MAUI app changes

- **Home screen** (new app entry point) shows category tiles built live from `GET /api/items/categories` plus an "All" tile — no category is hardcoded, so a third category would just appear once something's saved under it. "Extract New" (toolbar) is a separate, always-available action independent of what's already saved, since a brand-new install has zero tiles otherwise.
- **Extract screen** gained a Category picker (Food/Travel) that switches which endpoint is called and which fields are displayed/saved; the URL field validates/normalizes on unfocus.
- **List screen** gained a search bar (`/api/items/search`) and City/Area filter pickers (populated from `/api/items/locations`, scoped to whatever category the Home screen tile filtered to); each row shows a category badge.
- **Detail/Edit screen** shows a Food-fields panel or a Travel-fields panel depending on the item's category (never both at once).

### Running Phase 3

```bash
# Travel extraction
curl -X POST http://localhost:5235/api/extract/travel -H "Content-Type: application/json" -d '{
  "sourceUrl": "https://instagram.com/reel/waterfall123",
  "captionText": "Dudhsagar Falls, Goa - 4-tier waterfall, best June-Sept monsoon, jeep safari ~500/person."
}'

# Save it (same /api/items endpoint as Food, just travelData instead of foodData)
curl -X POST http://localhost:5235/api/items -H "Content-Type: application/json" -d '{
  "category": "Travel", "sourceUrl": "https://instagram.com/reel/waterfall123",
  "title": "Dudhsagar Falls", "travelData": { "placeName": "Dudhsagar Falls", "city": "Goa" }
}'

# Cross-category full-text search
curl "http://localhost:5235/api/items/search?q=waterfall"

# Filters (combinable)
curl "http://localhost:5235/api/items?category=Travel"
curl "http://localhost:5235/api/items?city=Goa"
curl "http://localhost:5235/api/items?category=Food&city=Bangalore"

# Category tiles / location dropdowns
curl "http://localhost:5235/api/items/categories"
curl "http://localhost:5235/api/items/locations?category=Travel"
```

### Phase 3 checklist

- [ ] `dotnet test` passes — Phase 1/2's 30 tests plus Phase 3's new travel-parsing/JSONB/dedupe/URL-validation/filter tests
- [ ] `dotnet ef database update` applies `AddLocationAndSearch`; `\d "SavedItems"` shows `Area`/`City` text columns and a `SearchVector` `tsvector` column marked `generated always as (...) stored`, plus a `gin` index on it
- [ ] `POST /api/extract/travel` on a travel caption returns structured data with `null` for anything not in the caption (no fabrication); saving it via `POST /api/items` stores it in the same `SavedItems` table with `Category = "Travel"`
- [ ] `GET /api/items/search?q=<term>` shared between a Food and a Travel item returns both, ranked — proves cross-category Postgres FTS, not client-side filtering
- [ ] `?category=Travel` returns only Travel items; `?city=...` filters correctly; `?category=Food&city=...` (combined) works; `/api/items/categories` returns categories with counts; `/api/items/locations` returns distinct cities/areas
- [ ] `InstagramUrlValidator` accepts a real `/reel/...` URL, rejects a non-Instagram URL with a reason, and normalizes a messy URL (tracking params stripped) — shown as explicit before/after output, not just "tests passed"
- [ ] Searching the repo for your Gemini key's actual prefix returns nothing outside this checklist line itself
- [ ] The MAUI app builds for `net10.0-maccatalyst`; Home (data-driven tiles) → category-filtered List (search + location filters) → Detail (Food or Travel panel) all navigate correctly

**Pass criterion:** all boxes checked, full-text search genuinely runs in Postgres (not fetched-then-filtered in C#), the category/location endpoints are truly data-driven (no hardcoded category list anywhere in API or app), and Travel data never touches a separate table or column — it's JSONB in the same `SavedItems` row as Food.

## Phase 4a: Android build target + deploy to a physical phone

Environment/deployment only — no new features, no share sheet (that's Phase 4b). The existing app now also builds and runs on Android; Mac Catalyst is untouched.

### One-time Android tool setup

1. **`maui-android` workload** — adds to (never replaces) `maui-maccatalyst`:

   ```bash
   sudo dotnet workload install maui-android
   ```

   Needs `sudo` because `/usr/local/share/dotnet` is root-owned (from the official .pkg installer) on this machine. Multi-GB download. Confirm with `dotnet workload list` — should show both `maui-android` and `maui-maccatalyst`.

   > While this is mid-install (or if you ever remove `maui-android` later), even `dotnet build -f net10.0-maccatalyst` will fail with `NETSDK1147` — MSBuild checks that *every* TFM in a multi-targeted project's `TargetFrameworks` list has its workload installed before building *any single one* of them, even with `-f`. This is standard behavior, not a break in the Catalyst target; it resolves the moment the install finishes.

2. **JDK + Android SDK** — unlike Mac Catalyst (which just needs Xcode), Android builds need a real JDK and the Android SDK/build-tools, and `dotnet build` does not auto-provision either of these on macOS. Installed here to user-owned locations (no `sudo`, no Android Studio required):

   ```bash
   # JDK 17 (Microsoft build, matches what .NET's Android tooling expects)
   curl -sL https://aka.ms/download-jdk/microsoft-jdk-17-macos-aarch64.tar.gz -o /tmp/msjdk17.tar.gz
   mkdir -p ~/Library/Android/jdk
   tar -xzf /tmp/msjdk17.tar.gz -C ~/Library/Android/jdk --strip-components=1
   # JAVA_HOME ends up at ~/Library/Android/jdk/jdk-<version>/Contents/Home - check with:
   find ~/Library/Android/jdk -maxdepth 2 -name Home

   # Android command-line tools (sdkmanager) - version number changes over time; find the
   # current one in Google's manifest rather than hardcoding it:
   curl -s https://dl.google.com/android/repository/repository2-3.xml | grep -o 'commandlinetools-mac-[0-9]*_latest.zip' | sort -u | tail -1
   curl -sL "https://dl.google.com/android/repository/<name-from-above>" -o /tmp/cmdline-tools.zip
   mkdir -p ~/Library/Android/sdk/cmdline-tools
   cd ~/Library/Android/sdk/cmdline-tools && unzip -q /tmp/cmdline-tools.zip && mv cmdline-tools latest

   # Install platform-tools (adb), the platform, and build-tools; accept licenses non-interactively
   export JAVA_HOME=~/Library/Android/jdk/jdk-17.0.20+8/Contents/Home   # adjust to your extracted version
   export PATH="$JAVA_HOME/bin:$HOME/Library/Android/sdk/cmdline-tools/latest/bin:$PATH"
   yes | sdkmanager --licenses
   sdkmanager "platform-tools" "platforms;android-36" "build-tools;36.1.0"
   ```

   Pick the platform/build-tools version to match whatever `Microsoft.Android.Ref.*`/`Microsoft.Android.Runtime.*.android` packs the installed workload references (`find /usr/local/share/dotnet/packs -maxdepth 1 -iname "*android.ref*"` shows the number — `36` here).

### Target framework

`ReelVault.App.csproj`: `<TargetFrameworks>net10.0-maccatalyst;net10.0-android</TargetFrameworks>` — added, Catalyst kept. `SupportedOSPlatformVersion` had to become per-platform (a single value doesn't make sense across a Catalyst OS version and an Android API level):
```xml
<SupportedOSPlatformVersion Condition="'$(TargetFramework)' == 'net10.0-maccatalyst'">15.0</SupportedOSPlatformVersion>
<SupportedOSPlatformVersion Condition="'$(TargetFramework)' == 'net10.0-android'">21.0</SupportedOSPlatformVersion>
```
`Platforms/Android/` (MainActivity.cs, MainApplication.cs, AndroidManifest.xml, Resources/values/colors.xml) didn't exist — it was removed in Phase 0 when the app went Catalyst-only — so it's recreated here from the standard MAUI template, namespaced to `ReelVault.App`.

### Networking: base URL, cleartext, and binding

Three pieces have to agree with each other — the app's base URL, the Android cleartext exception, and what the API binds to:

1. **App base URL** (`MauiProgram.cs`) — one `ApiBaseUrl` constant, platform-conditional:
   ```csharp
   private const string ApiBaseUrl =
   #if ANDROID
       "http://192.168.0.105:5235";   // Mac's Wi-Fi IP (ipconfig getifaddr en0) - a physical phone can't use 127.0.0.1, that's the phone itself
   #else
       "http://127.0.0.1:5235";       // Catalyst runs on the same machine as the API
   #endif
   ```
   This IP is DHCP-assigned and **will change** if your Mac reconnects to Wi-Fi or the router reassigns leases. Re-check with `ipconfig getifaddr en0` and update both this constant and the network security config below if it does.

2. **Android cleartext HTTP** — Android blocks plain HTTP by default. `Platforms/Android/Resources/xml/network_security_config.xml` permits cleartext to *only* the Mac's IP (not a blanket allow), referenced from `AndroidManifest.xml` via `android:networkSecurityConfig="@xml/network_security_config"`:
   ```xml
   <network-security-config>
       <domain-config cleartextTrafficPermitted="true">
           <domain includeSubdomains="false">192.168.0.105</domain>
       </domain-config>
   </network-security-config>
   ```

3. **API binding** — must listen on all interfaces, not just loopback, so the phone (a different device on the LAN) can reach it:
   ```bash
   cd ReelVault.Api
   ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://0.0.0.0:5235
   ```

   **macOS firewall:** the firewall is on for this machine, and `dotnet` isn't yet in its allowed-apps list. The first time an *external* device (the phone) actually connects, macOS may pop up "Do you want the application 'dotnet' to accept incoming network connections?" — click **Allow**. (Mac-to-itself traffic via the LAN IP doesn't reliably trigger this same prompt, so don't rely on a same-machine `curl` test to rule it out.)

### Building and deploying to the phone

`ReelVault.App.csproj` defaults `AndroidSdkDirectory`/`JavaSdkDirectory` to the Phase 4a install locations above (only applied when the Android TFM is building and those properties aren't already set), so plain `dotnet build`/`dotnet test`/`dotnet build -t:Run` all work without extra flags:

```bash
# Confirm the phone is visible and authorized (tap "Allow" on the phone's USB debugging
# prompt if it shows "unauthorized")
export PATH="$HOME/Library/Android/sdk/platform-tools:$PATH"
adb devices -l

# Build
cd ReelVault.App
dotnet build -f net10.0-android

# Build + install + launch on the connected device
dotnet build -t:Run -f net10.0-android
```

If you install the JDK/SDK to different paths (or on a different machine/CI), override with `-p:AndroidSdkDirectory=...` / `-p:JavaSdkDirectory=...` — explicit flags win over the csproj defaults. Without either, the build fails with `XA5300` (SDK/JDK not found) or `XA0030` (wrong JDK version — macOS's `java_home` search can surface an unrelated newer JDK before the required 21.x/17 one).

### Testing on the phone

1. `docker compose up -d` (Postgres)
2. `cd ReelVault.Api && dotnet ef database update` (if not already applied)
3. `ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://0.0.0.0:5235` — leave running
4. Click **Allow** if a macOS firewall prompt appears once the phone connects
5. Open ReelVault on the phone, paste a caption, tap Extract, then Save, then check Saved Items — should behave identically to Catalyst, just talking to the Mac over Wi-Fi instead of loopback

### Phase 4a checklist

- [ ] `dotnet workload list` shows both `maui-android` and `maui-maccatalyst`
- [ ] `dotnet build -f net10.0-maccatalyst` still succeeds (Catalyst untouched)
- [ ] `dotnet build -f net10.0-android` succeeds with the `AndroidSdkDirectory`/`JavaSdkDirectory` properties shown above
- [ ] `adb devices -l` shows the phone as `device` (not `unauthorized`)
- [ ] `dotnet build -t:Run -f net10.0-android` installs and launches the app on the phone (`adb shell pm list packages | grep reelvault` shows it installed)
- [ ] With the API bound to `0.0.0.0` and Docker/Postgres up, the app on the phone can Extract, Save, and see the item in Saved Items — same behavior as Catalyst

**Pass criterion:** the existing app runs unmodified-in-behavior on the physical Android phone, reaches the Mac's API over Wi-Fi, and extract/save/list all work — while Mac Catalyst continues to work exactly as before.

## Phase 4b: Instagram share sheet on Android

Android-only. Sharing a reel (from Instagram or a browser) to ReelVault now opens the Extract screen with the link and any accompanying text already filled in — no more manual copy-paste. No fetching/scraping of Instagram: this only reads the text the OS share `Intent` hands over.

### How it's wired

- **`ReelVault.Shared/ShareTextParser.cs`** — pure, network-free parsing: given the raw shared text, finds an Instagram URL in it (reusing `InstagramUrlValidator`, no duplicated URL logic), separates it from any surrounding text, and returns both plus whether a valid URL was found. Testable without Android — see `ReelVault.Api.Tests/ShareTextParserTests.cs`.
- **`ReelVault.App/Services/ISharedContentService.cs`** — a small pub/sub service (`SharedContent` record + `ContentReceived` event), registered as a singleton in `MauiProgram.cs`. This is the platform-agnostic hand-off point: Android's `MainActivity` is the only thing that publishes to it today, but nothing here is Android-specific.
- **`ReelVault.App/Platforms/Android/MainActivity.cs`** — registers the share target via `[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "text/plain")]` (MAUI's manifest-merging from a C# attribute, not a hand-edited `AndroidManifest.xml`). Handles both:
  - **cold start** — `OnCreate` reads `Intent` after `base.OnCreate` (the Maui app/window already exist by then).
  - **warm start** — `LaunchMode.SingleTop` means a share while the app is already running redelivers via `OnNewIntent` instead of a new `OnCreate`; both call the same `HandleShareIntent` helper.

  It reads `Intent.ExtraText` and `Intent.ExtraSubject` (joined if both present), runs it through `ShareTextParser`, and publishes a `SharedContent` to the service — nothing more.
- **`ReelVault.App/App.xaml.cs`** — subscribes to `ContentReceived` once, in the constructor. On a share, it resolves a *fresh* `MainPage` from DI, calls `PrefillFromShare(content)` on it, then pushes it onto the existing `NavigationPage`. Fresh instance each time (MainPage is `AddTransient`) so re-sharing while a stale Extract screen is still open doesn't reuse stale state.
- **`ReelVault.App/MainPage.xaml(.cs)`** — `PrefillFromShare` fills `SourceUrlEntry`/`CaptionEditor` from whatever arrived, and shows one of three hints in a new `ShareHintLabel`:
  - URL only, no caption → *"Link shared from Instagram — paste the caption text below, then tap Extract."*
  - Non-Instagram text only → *"Shared text didn't look like an Instagram link, so it's been added to the caption. Paste a link above if you have one."*
  - Both URL and caption text → *"Shared from Instagram — review the details below, then tap Extract."*

  If the share is empty or isn't `ACTION_SEND`/`text/plain`, `MainActivity` never publishes anything — the app just opens normally to Home.

### Evidence

**1. Merged manifest intent-filter** (from `ReelVault.App/obj/Debug/net10.0-android/AndroidManifest.xml` after building):
```xml
<activity android:configChanges="..." android:launchMode="singleTop" android:theme="@style/Maui.SplashTheme" android:name="crc646aec0a29bc220656.MainActivity" android:exported="true">
  <intent-filter>
    <action android:name="android.intent.action.MAIN" />
    <category android:name="android.intent.category.LAUNCHER" />
  </intent-filter>
  <intent-filter>
    <action android:name="android.intent.action.SEND" />
    <category android:name="android.intent.category.DEFAULT" />
    <data android:mimeType="text/plain" />
  </intent-filter>
</activity>
```

**2. Tests** — `dotnet test`: **75/75 passing** (66 pre-existing + 9 new `ShareTextParserTests` covering URL-only, URL+text, non-Instagram text, non-Instagram URL, empty/null/whitespace, trailing punctuation, and post vs. reel URLs).

**3. Both targets build clean** — `dotnet build -f net10.0-android` and `dotnet build -f net10.0-maccatalyst`: 0 warnings, 0 errors, both using the Phase 4a csproj JDK/SDK defaults unchanged.

**4. On-device verification** (via `adb shell am start`, simulating exactly what Instagram's share sheet delivers):
- Cold start: force-stopped the app, then sent an explicit `ACTION_SEND`/`text/plain` intent with just a URL → app launched straight to the Extract screen, `Reel URL` field pre-filled with the normalized URL, hint showed the "paste the caption" message, caption field empty as expected.
- Warm start: with the app already in foreground, sent a second `ACTION_SEND` intent with a URL + surrounding text → adb itself reported *"intent has been delivered to currently running top-most instance"* (confirming `OnNewIntent`, not `OnCreate`, handled it), and the UI showed both fields correctly split and pre-filled, with the "review the details" hint.

Both confirmed via `adb shell uiautomator dump` (screen-content XML), not just "build succeeded."

### Testing on the phone yourself

1. With the app installed (`dotnet build -t:Run -f net10.0-android` from `ReelVault.App/`) and the API running per Phase 4a:
2. **Cold-start test**: force-close ReelVault (swipe it away from recent apps, or `adb shell am force-stop com.companyname.reelvault.app`). Open Instagram (or a browser showing an Instagram reel/post URL), tap **Share**, pick **ReelVault** from the share sheet. The app should launch directly to the Extract screen with the link pre-filled.
3. **Warm-start test**: with ReelVault still open (don't force-close it), go back to Instagram/browser and share another reel to ReelVault again. It should come to the foreground immediately (no relaunch animation) with a *new* Extract screen pre-filled for the new link — this exercises `OnNewIntent` instead of `OnCreate`.
4. Either way: review/edit the pre-filled URL and caption, pick Food or Travel, tap Extract, then Save — same downstream flow as manual entry.
5. Try sharing plain text with no link (e.g. select some text in Notes and share it) to confirm graceful handling — it should land in the caption field with the "didn't look like an Instagram link" hint, not crash.

### Phase 4b checklist

- [ ] Merged `AndroidManifest.xml` shows the `SEND`/`DEFAULT`/`text/plain` intent-filter alongside the launcher one
- [ ] `dotnet test` — 75/75 passing, including new `ShareTextParserTests`
- [ ] `dotnet build -f net10.0-android` and `dotnet build -f net10.0-maccatalyst` both succeed, 0 errors
- [ ] Cold-start share (app force-stopped) opens directly to Extract screen, pre-filled
- [ ] Warm-start share (app already open) updates to a fresh pre-filled Extract screen via `OnNewIntent`
- [ ] Non-Instagram shared text still pre-fills gracefully (caption only, gentle hint, no crash)
- [ ] Mac Catalyst still builds and runs exactly as before — no share sheet there, as expected

**Pass criterion:** sharing an Instagram reel (from Instagram or a browser) to ReelVault opens the Extract screen with the URL and any accompanying text pre-filled, for both cold and warm app states — with no scraping/fetching, no duplicated URL logic, and Mac Catalyst/Android networking from Phase 4a untouched.
