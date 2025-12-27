# WeeklyDiet

Local-first weekly meal planner with grocery list support, built with ASP.NET Core 9 Minimal APIs, EF Core, PostgreSQL (Supabase), and a lightweight vanilla HTML/CSS/JS front-end.

## Features
- Manage ingredients and foods (unique names, meal-type constraints).
- Generate weekly plans (current + upcoming) on demand via UI; respects meal types, minimizes duplicates, evenly distributes unavoidable repeats.
- Replace individual meals and mark meals as leftovers for the next day (current week only, including Sunday → next week Monday).
- Grocery list per plan (unique ingredient list) that updates after replacements/leftovers.
- Single-process app serving both API and UI; light enough for low-power devices.

## Tech Stack
- Backend: ASP.NET Core 9 (Minimal API), EF Core 9, PostgreSQL (Supabase).
- Frontend: Vanilla HTML/CSS/JS served from `wwwroot`.
- Hosting: Single Kestrel process; no auth; local network only.

## Getting Started (Local)
1. Prerequisites: .NET 9 SDK, PostgreSQL connection (Supabase).
2. Restore/build:
   ```bash
   dotnet restore
   dotnet run --project WeeklyDiet.Api
   ```
3. Open the UI at http://localhost:5005 (default from `launchSettings.json`). Static files are under `WeeklyDiet.Api/wwwroot`.
4. Configure DB: set env var `SUPABASE_CONNECTION` (e.g. `postgresql://postgres:gK4OaoCNKaQfu7k6@db.zrkqputinqsepgjbayjk.supabase.co:5432/postgres`). `appsettings.json` has a local fallback; the env var wins.

### Run with Docker (local or Render)
```bash
docker build -t weeklydiet .
docker run -p 8080:8080 -e SUPABASE_CONNECTION=postgresql://postgres:gK4OaoCNKaQfu7k6@db.zrkqputinqsepgjbayjk.supabase.co:5432/postgres weeklydiet
```
Then open http://localhost:8080.

### API Highlights
- Ingredients: `GET/POST/PUT/DELETE /api/ingredients`
- Foods: `GET/POST/PUT/DELETE /api/foods`
- Plans: `GET /api/plans/current`, `GET /api/plans/upcoming`, `POST /api/plans/upcoming/generate`
- Replace meal: `POST /api/plans/{year}/{week}/days/{day}/meals/{mealType}/replace?foodId=`
- Toggle leftover: `POST /api/plans/{year}/{week}/days/{day}/meals/{mealType}/leftover?isLeftover=`
- Grocery list: `GET /api/plans/{year}/{week}/grocery-list`
- `day`: 1 = Monday … 7 = Sunday. `mealType`: `Breakfast|Lunch|Dinner`.

## Project Layout
- `WeeklyDiet.Api/Program.cs` – Minimal API endpoints and configuration.
- `WeeklyDiet.Api/Models` – EF Core entities.
- `WeeklyDiet.Api/Data` – DbContext and configuration.
- `WeeklyDiet.Api/DTOs` – API boundary models.
- `WeeklyDiet.Api/Utilities/PlanningService.cs` – Plan generation, replacements, leftovers.
- `WeeklyDiet.Api/wwwroot` – Frontend HTML/CSS/JS.
- `.github/workflows/dotnet.yml` – CI build + publish artifact (Linux ARM).

## GitHub Actions CI
- Trigger: push to `main`.
- Steps: checkout → setup .NET 9 → restore → build (Release) → publish self-contained `linux-arm` → upload artifact.
- Artifact: `publish-linux-arm` folder ready for manual deploy on ARM devices.

## Render (Docker) deployment
- Service type: Web Service (Docker).
- Build: Render uses the root `Dockerfile`.
- Port: 8080 (exposed in Dockerfile); Render auto-detects.
- Env vars:
  - `SUPABASE_CONNECTION=postgresql://postgres:gK4OaoCNKaQfu7k6@db.zrkqputinqsepgjbayjk.supabase.co:5432/postgres`
  - `ASPNETCORE_URLS` already set in Dockerfile to `http://+:8080`.
- Health check: `/api/health`.
- Migrations: run once via a Render Job or shell using `dotnet ef database update -p WeeklyDiet.Api -s WeeklyDiet.Api` with the same env vars.

## Operational Notes
- No authentication by design; keep network limited.
- Leftovers only configurable on the current week; they affect the immediate next day (Sunday can feed next Monday).
- To reset data, stop the service and remove `Data/weeklydiet.db`.
- For new foods/ingredients, add at least one item per meal type before generating plans.

## License
MIT (replace with your preferred license if needed).
