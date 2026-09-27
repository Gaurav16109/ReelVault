# ReelVault

**Turn saved Instagram Reels into a searchable database of places you actually want to visit.**

You save a reel about a cafe. Two weeks later you're in that neighbourhood and have no idea which one it was — because a saved video isn't searchable. ReelVault fixes that: share a reel into the app, and it becomes a structured, searchable, map-linked entry you can find when it matters.

---

## How it works

1. **Share** — You share an Instagram Reel to ReelVault from the Android share sheet. One tap, no copy-pasting.
2. **Extract** — The caption is sent to Gemini with a strict schema. It pulls out place name, area, city, cuisine, must-try dishes, price, hours. **Anything the caption doesn't say comes back `null`** — the model is explicitly forbidden from guessing.
3. **Enrich** — The app automatically looks the place up on Google Places to fill in what the caption didn't mention: rating, review count, price level, opening hours, address, coordinates, a Maps link.
4. **Find it later** — Browse by category, search across everything, filter by city or area, and see at a glance whether a place is open right now.

---

## The interesting problem: knowing when *not* to trust a match

Place names are ambiguous. "Coffee House" matches five real places in one city. Matching the wrong one is worse than matching nothing — wrong opening hours sends someone across town to a closed restaurant, and you lose that user permanently.

So enrichment runs through a **confidence scorer** rather than blindly taking Google's first result. It scores candidates on name similarity, whether the candidate's location agrees with the area/city from the caption, and the gap between the top two candidates. That produces three behaviours:

| Confidence | Behaviour |
|---|---|
| **High** | Enrich automatically and silently. |
| **Ambiguous** (2+ viable candidates) | Store **nothing**. Show the user a picker with each candidate's name, address and rating so they can choose — location is what lets a human tell them apart. |
| **No viable match** | Leave the fields empty with an honest "couldn't confidently match" state. |

The governing principle throughout: **be right, or be honest that you're unsure — never be confidently wrong.**

The same principle applies to extraction. Data from the reel and data from Google are stored separately, labelled separately in the UI, and never merged — so the user can always see where a given fact came from. Every item also keeps the original caption and the raw model output, so anything can be re-checked or re-processed later.

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  ReelVault.App  —  .NET MAUI (Android + Mac Catalyst)│
│  Share target · category home · browse · detail/edit │
└───────────────────────┬─────────────────────────────┘
                        │ HTTP (runtime-configurable base URL)
┌───────────────────────▼─────────────────────────────┐
│  ReelVault.Api  —  ASP.NET Core                      │
│  ILlmExtractor · IPlaceEnricher · items CRUD ·       │
│  dedupe · full-text search · confidence scoring      │
└──────┬──────────────────────────────┬────────────────┘
       │                              │
┌──────▼────────┐          ┌──────────▼────────────────┐
│ PostgreSQL 16 │          │ Gemini API (extraction)   │
│ (Docker)      │          │ Google Places (enrichment)│
│ SavedItems    │          └───────────────────────────┘
│ + JSONB + FTS │
└───────────────┘
```

The app is a thin client — it never touches the database or an external API directly. Everything goes through the API, which is also where the API keys live, so no key ever ships inside the app binary.

---

## Key design decisions

**One table with JSONB, not a table per category.**
Every saved item is the same *kind* of object — a place saved from a reel, with a title, status, notes, city, timestamps. Only the category-specific fields differ (Food has cuisine and must-try; Travel has place-type and best-time-to-visit). So shared fields are real columns and category-specific fields live in a `CategoryData` JSONB column.

The alternative — a table per category — would turn every cross-category feature (search, "recently saved", browse-everything, near-me) into a union query that grows with each new category. This design was tested: **adding the entire Travel category later required zero schema changes** and zero changes to CRUD, dedupe or soft-delete.

`Area` and `City` are the exception — promoted to real indexed columns, because every category has them and they're the hot filter path.

**Postgres full-text search, not client-side filtering.**
A `GENERATED ALWAYS AS ... STORED` tsvector column over title, summary, area, city and the category-specific text pulled out of JSONB, with a GIN index. Ranked by `ts_rank`, and it works across categories in one query with no per-category branching.

**Caption-first ingestion — no scraping.**
There's no official Instagram API for arbitrary reels, and scraping would violate their terms, break constantly, and risk blocking. ReelVault only ever processes what the OS share intent hands it. That constraint shaped the product: it's why extraction is caption-based, and a large part of why the enrichment layer exists — enrichment is how useful data is recovered when the share gives very little.

**Soft delete, never hard delete.**
The whole point of the app is not losing saved content, so deleting sets a flag and the row stays.

**Dedupe warns, never blocks.**
Saving a place you already have returns a duplicate signal with the existing item, so the user can update it or save anyway. Silently refusing feels broken; silently overwriting loses data.

**Swappable external dependencies.**
`ILlmExtractor`, `IPlaceEnricher`, `IPlacePhotoFetcher` and `IThumbnailFetcher` are all interfaces. Model providers and data sources change; the architecture shouldn't have to.

---

## Tech stack

| Layer | Technology |
|---|---|
| Mobile app | .NET MAUI 10 (Android, Mac Catalyst) |
| Backend | ASP.NET Core 10 Web API |
| Database | PostgreSQL 16 (Docker), JSONB + tsvector/GIN |
| ORM | Entity Framework Core (Npgsql) |
| AI extraction | Google Gemini API |
| Place enrichment | Google Places API (New) |
| Tests | xUnit — 163 tests |
| Secrets | .NET user-secrets |

---

## API

| Endpoint | Purpose |
|---|---|
| `POST /api/extract/food` | Caption → structured food data |
| `POST /api/extract/travel` | Caption → structured travel data |
| `POST /api/items` | Save an item (runs dedupe check) |
| `GET /api/items` | List, with combinable `?q=` `?category=` `?city=` `?area=` filters |
| `GET /api/items/search?q=` | Cross-category Postgres full-text search, ranked |
| `GET /api/items/{id}` | Full detail |
| `PUT /api/items/{id}` | Edit fields, notes, status |
| `DELETE /api/items/{id}` | Soft delete (row is retained) |
| `POST /api/items/{id}/enrich` | Run Places enrichment with confidence scoring |
| `POST /api/items/{id}/enrich/select` | Resolve an ambiguous match by place ID |
| `GET /api/items/categories` | Categories with counts (drives the home screen) |
| `GET /api/items/locations` | Distinct cities/areas (drives filter dropdowns) |

---

## Running it locally

**Prerequisites:** .NET 10 SDK, Docker Desktop, the `maui-maccatalyst` and/or `maui-android` workloads, Xcode (for Mac Catalyst), a Gemini API key and a Google Places API key.

```bash
# 1. Secrets (never committed — stored outside the project)
cd ReelVault.Api
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-key>"
dotnet user-secrets set "Places:ApiKey" "<your-places-key>"
cd ..

# 2. Database
docker compose up -d

# 3. Migrations
cd ReelVault.Api && dotnet ef database update

# 4. API  (use 0.0.0.0 if testing from a physical phone on the same Wi-Fi)
dotnet run --urls http://0.0.0.0:5235

# 5. App
cd ../ReelVault.App
dotnet build -t:Run -f net10.0-maccatalyst      # desktop
dotnet build -t:Run -f net10.0-android          # connected Android device
```

The API base URL is a **runtime setting** in the app (Settings screen), so pointing it at a different host doesn't require a rebuild.

```bash
dotnet test     # 163 tests
```

---

## Known limitations

Being upfront about what this does and doesn't do:

- **Single user.** No authentication. `SavedItem` carries a nullable `UserId` column specifically so multi-user is a migration and a query filter rather than a redesign.
- **Not deployed.** Runs locally; the API and Postgres would need hosting, and secrets would move to a platform secret store.
- **Instagram thumbnails unavailable.** Instagram's oEmbed requires a Facebook Graph API token from an approved app. `IThumbnailFetcher` is isolated so a token can be dropped in later; meanwhile cards use the Google Places photo when enriched, and a designed placeholder otherwise.
- **Extraction depends on caption quality.** When a share carries no caption (common, and Instagram captions can't easily be copied), the app asks for place name + location instead and lets enrichment do the rest.
- **Confidence thresholds are heuristics.** They're unit-tested for the clear cases but not tuned against a labelled dataset — that needs real usage data, which the design already collects the right signal for (auto-match overrides and picker selections).
- **No offline support, and no pagination yet** — the first thing I'd add before real scale.

## What's next

- Multi-user auth and a hosted deployment
- Screenshot-based capture (user-taken screenshots through vision OCR — legitimate, unlike scraping) for reels where the information is only in the video
- Geo-distance "near me" sorting, now that enrichment supplies coordinates
- Map view of saved places

---

## Build log

This was built in phases, each with a defined scope and a pass criterion: foundation → extraction → persistence → multi-category + search → share sheet → enrichment → UI. No phase was considered done until its automated tests passed *and* it was manually verified on a real device.

The detailed phase-by-phase log — including setup steps, the bugs found, and what each phase proved — is in [`docs/BUILD_LOG.md`](docs/BUILD_LOG.md).

---

## Author

**Gaurav Kelwade** — [github.com/Gaurav16109](https://github.com/Gaurav16109)
