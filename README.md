# WeeklyDiet

Local-first weekly meal planner with grocery list support, built with ASP.NET Core 9 Minimal APIs, EF Core, PostgreSQL (Supabase), and a lightweight vanilla HTML/CSS/JS front-end.

## Features
- Manage ingredients and foods (unique names, meal-type constraints).
- Generate weekly plans (current + upcoming) on demand via UI; respects meal types, minimizes duplicates, evenly distributes unavoidable repeats.
- Replace individual meals and mark meals as leftovers for the next day (current week only, including Sunday → next week Monday).
- Grocery list per plan (unique ingredient list) that updates after replacements/leftovers.
- Single-process app serving both API and UI; optimized for low-power devices (e.g., Raspberry Pi B+).

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
- Artifact: `publish-linux-arm` folder ready for manual deploy on Raspberry Pi.

## Raspberry Pi B+ Deployment (manual)
Assumes Raspberry Pi OS (32-bit) with network access to Supabase.

1) Install .NET runtime (9.x, armv7l)
```bash
wget https://download.visualstudio.microsoft.com/download/pr/9.0.0/dotnet-runtime-9.0.0-linux-arm.tar.gz -O dotnet-runtime.tar.gz
sudo mkdir -p /usr/share/dotnet
sudo tar -xf dotnet-runtime.tar.gz -C /usr/share/dotnet
echo 'export DOTNET_ROOT=/usr/share/dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:/usr/share/dotnet' >> ~/.bashrc
source ~/.bashrc
dotnet --info
```

2) Get the app
```bash
cd /opt
sudo git clone <repo-url> weeklydiet
cd weeklydiet
sudo dotnet publish WeeklyDiet.Api -c Release -r linux-arm --self-contained false -o /opt/weeklydiet/publish
```

3) Configure Supabase connection (env var)
```bash
echo 'SUPABASE_CONNECTION=postgresql://postgres:gK4OaoCNKaQfu7k6@db.zrkqputinqsepgjbayjk.supabase.co:5432/postgres' | sudo tee -a /etc/environment
```

4) systemd service (`/etc/systemd/system/weeklydiet.service`)
```
[Unit]
Description=Weekly Diet Planner
After=network.target

[Service]
WorkingDirectory=/opt/weeklydiet/publish
ExecStart=/usr/share/dotnet/dotnet /opt/weeklydiet/publish/WeeklyDiet.Api.dll
Restart=always
User=pi
Environment=ASPNETCORE_URLS=http://0.0.0.0:5005
Environment=SUPABASE_CONNECTION=${SUPABASE_CONNECTION}

[Install]
WantedBy=multi-user.target
```
```bash
sudo systemctl daemon-reload
sudo systemctl enable weeklydiet --now
sudo systemctl status weeklydiet
```

5) Access on LAN: http://<raspberrypi-ip>:5005

## Operational Notes
- No authentication by design; keep network limited.
- Leftovers only configurable on the current week; they affect the immediate next day (Sunday can feed next Monday).
- To reset data, stop the service and remove `Data/weeklydiet.db`.
- For new foods/ingredients, add at least one item per meal type before generating plans.

## License
MIT (replace with your preferred license if needed).
