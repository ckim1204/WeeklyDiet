using System.Net;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WeeklyDiet.Api.Data;
using WeeklyDiet.Api.DTOs;
using WeeklyDiet.Api.Models;
using WeeklyDiet.Api.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string not configured. Set SUPABASE_CONNECTION.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(NormalizeConnectionString(connectionString)));

builder.Services.AddScoped<PlanningService>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Ingredients
app.MapGet("/api/ingredients", async (AppDbContext db, CancellationToken ct) =>
{
    var items = await db.Ingredients
        .OrderBy(i => i.Name)
        .Select(i => new IngredientDto(i.Id, i.Name))
        .ToListAsync(ct);
    return Results.Ok(items);
});

app.MapPost("/api/ingredients", async (IngredientCreateUpdateDto dto, AppDbContext db, CancellationToken ct) =>
{
    var name = dto.Name.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Name is required.");
    }

    var exists = await db.Ingredients.AnyAsync(i => i.Name.ToLower() == name.ToLower(), ct);
    if (exists)
    {
        return Results.Conflict("Ingredient name must be unique.");
    }

    var ingredient = new Ingredient { Name = name };
    db.Ingredients.Add(ingredient);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/ingredients/{ingredient.Id}", new IngredientDto(ingredient.Id, ingredient.Name));
});

app.MapPut("/api/ingredients/{id:int}", async (int id, IngredientCreateUpdateDto dto, AppDbContext db, CancellationToken ct) =>
{
    var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
    if (ingredient is null)
    {
        return Results.NotFound();
    }

    var name = dto.Name.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Name is required.");
    }

    var exists = await db.Ingredients.AnyAsync(i => i.Id != id && i.Name.ToLower() == name.ToLower(), ct);
    if (exists)
    {
        return Results.Conflict("Ingredient name must be unique.");
    }

    ingredient.Name = name;
    await db.SaveChangesAsync(ct);
    return Results.Ok(new IngredientDto(ingredient.Id, ingredient.Name));
});

app.MapDelete("/api/ingredients/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
{
    var ingredient = await db.Ingredients.Include(i => i.Foods).FirstOrDefaultAsync(i => i.Id == id, ct);
    if (ingredient is null)
    {
        return Results.NotFound();
    }

    var inUse = await db.FoodIngredients.AnyAsync(fi => fi.IngredientId == id, ct);
    if (inUse)
    {
        return Results.BadRequest("Cannot delete ingredient that is used by foods.");
    }

    db.Ingredients.Remove(ingredient);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

// Foods
app.MapGet("/api/foods", async (AppDbContext db, CancellationToken ct) =>
{
    var foods = await db.Foods
        .Include(f => f.Ingredients)
        .ThenInclude(fi => fi.Ingredient)
        .OrderBy(f => f.Name)
        .ToListAsync(ct);

    var result = foods.Select(f => new FoodDto(
        f.Id,
        f.Name,
        Enum.GetValues<MealType>().Where(mt => mt != MealType.None && f.AllowedMealTypes.HasFlag(mt)).Select(mt => mt.ToString()).ToList(),
        f.Ingredients.Select(fi => fi.IngredientId).ToList(),
        f.Ingredients.Select(fi => new IngredientDto(fi.IngredientId, fi.Ingredient?.Name ?? string.Empty)).ToList()
    ));

    return Results.Ok(result);
});

app.MapPost("/api/foods", async (FoodCreateUpdateDto dto, AppDbContext db, CancellationToken ct) =>
{
    var name = dto.Name.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Name is required.");
    }

    if (dto.AllowedMealTypes == null || dto.AllowedMealTypes.Count == 0)
    {
        return Results.BadRequest("At least one meal type is required.");
    }

    var allowed = dto.AllowedMealTypes.Aggregate(MealType.None, (current, next) => current | next);
    if (allowed == MealType.None)
    {
        return Results.BadRequest("Invalid meal types.");
    }

    var ingredientIds = dto.IngredientIds.Distinct().ToList();
    var ingredients = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToListAsync(ct);
    if (ingredients.Count != ingredientIds.Count)
    {
        return Results.BadRequest("One or more ingredients were not found.");
    }

    var exists = await db.Foods.AnyAsync(f => f.Name.ToLower() == name.ToLower(), ct);
    if (exists)
    {
        return Results.Conflict("Food name must be unique.");
    }

    var food = new Food
    {
        Name = name,
        AllowedMealTypes = allowed
    };

    foreach (var ingredientId in ingredientIds)
    {
        food.Ingredients.Add(new FoodIngredient { IngredientId = ingredientId });
    }

    db.Foods.Add(food);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/foods/{food.Id}", new { food.Id });
});

app.MapPut("/api/foods/{id:int}", async (int id, FoodCreateUpdateDto dto, AppDbContext db, CancellationToken ct) =>
{
    var food = await db.Foods
        .Include(f => f.Ingredients)
        .FirstOrDefaultAsync(f => f.Id == id, ct);

    if (food is null)
    {
        return Results.NotFound();
    }

    var name = dto.Name.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Name is required.");
    }

    if (dto.AllowedMealTypes == null || dto.AllowedMealTypes.Count == 0)
    {
        return Results.BadRequest("At least one meal type is required.");
    }

    var allowed = dto.AllowedMealTypes.Aggregate(MealType.None, (current, next) => current | next);
    if (allowed == MealType.None)
    {
        return Results.BadRequest("Invalid meal types.");
    }

    var ingredientIds = dto.IngredientIds.Distinct().ToList();
    var ingredients = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToListAsync(ct);
    if (ingredients.Count != ingredientIds.Count)
    {
        return Results.BadRequest("One or more ingredients were not found.");
    }

    var nameExists = await db.Foods.AnyAsync(f => f.Id != id && f.Name.ToLower() == name.ToLower(), ct);
    if (nameExists)
    {
        return Results.Conflict("Food name must be unique.");
    }

    food.Name = name;
    food.AllowedMealTypes = allowed;
    food.Ingredients.Clear();
    foreach (var ingredientId in ingredientIds)
    {
        food.Ingredients.Add(new FoodIngredient { IngredientId = ingredientId, FoodId = food.Id });
    }

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { food.Id });
});

app.MapDelete("/api/foods/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
{
    var food = await db.Foods.FirstOrDefaultAsync(f => f.Id == id, ct);
    if (food is null)
    {
        return Results.NotFound();
    }

    var inUse = await db.MealEntries.AnyAsync(m => m.FoodId == id || m.BaseFoodId == id || m.ManualFoodId == id, ct);
    if (inUse)
    {
        return Results.BadRequest("Cannot delete food that is used in a weekly plan.");
    }

    db.Foods.Remove(food);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

// Plans
app.MapGet("/api/plans/current", async (PlanningService planningService, CancellationToken ct) =>
{
    var (year, week) = DateHelpers.GetCurrentIsoWeek();
    var plan = await planningService.GetPlanAsync(year, week, ct);
    return plan is null ? Results.NotFound("No plan for the current week. Generate one first.") : Results.Ok(ToDto(plan));
});

app.MapGet("/api/plans/upcoming", async (PlanningService planningService, CancellationToken ct) =>
{
    var (year, week) = DateHelpers.GetUpcomingIsoWeek();
    var plan = await planningService.GetPlanAsync(year, week, ct);
    return plan is null ? Results.NotFound("No plan for the upcoming week. Generate one first.") : Results.Ok(ToDto(plan));
});

app.MapPost("/api/plans/current/generate", async (PlanningService planningService, CancellationToken ct) =>
{
    var (year, week) = DateHelpers.GetCurrentIsoWeek();
    var existing = await planningService.GetPlanAsync(year, week, ct);
    if (existing is not null)
    {
        return Results.Conflict("Current plan already exists.");
    }

    try
    {
        var plan = await planningService.GeneratePlanAsync(year, week, ct);
        return Results.Ok(ToDto(plan));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/plans/upcoming/generate", async (PlanningService planningService, CancellationToken ct) =>
{
    var (year, week) = DateHelpers.GetUpcomingIsoWeek();
    var existing = await planningService.GetPlanAsync(year, week, ct);
    if (existing is not null)
    {
        return Results.Conflict("Upcoming plan already exists.");
    }

    try
    {
        var plan = await planningService.GeneratePlanAsync(year, week, ct);
        return Results.Ok(ToDto(plan));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/plans/{year:int}/{week:int}", async (int year, int week, PlanningService planningService, CancellationToken ct) =>
{
    var plan = await planningService.GetPlanAsync(year, week, ct);
    return plan is null ? Results.NotFound() : Results.Ok(ToDto(plan));
});

app.MapPost("/api/plans/{year:int}/{week:int}/days/{day:int}/meals/{mealType}/replace", async (int year, int week, int day, MealType mealType, int foodId, PlanningService planningService, CancellationToken ct) =>
{
    if (day < 1 || day > 7)
    {
        return Results.BadRequest("Day must be between 1 (Monday) and 7 (Sunday).");
    }

    if (!IsValidMealType(mealType))
    {
        return Results.BadRequest("Invalid meal type.");
    }

    var entry = await planningService.ReplaceMealAsync(year, week, day, mealType, foodId, ct);
    return entry is null ? Results.BadRequest("Unable to replace meal. Ensure the plan, meal, and food exist and are compatible.") : Results.Ok();
});

app.MapPost("/api/plans/{year:int}/{week:int}/days/{day:int}/meals/{mealType}/leftover", async (int year, int week, int day, MealType mealType, bool isLeftover, PlanningService planningService, CancellationToken ct) =>
{
    if (day < 1 || day > 7)
    {
        return Results.BadRequest("Day must be between 1 (Monday) and 7 (Sunday).");
    }

    if (!IsValidMealType(mealType))
    {
        return Results.BadRequest("Invalid meal type.");
    }

    var (success, error) = await planningService.ToggleLeftoverAsync(year, week, day, mealType, isLeftover, ct);
    return success ? Results.Ok() : Results.BadRequest(error);
});

app.MapGet("/api/plans/{year:int}/{week:int}/grocery-list", async (int year, int week, AppDbContext db, PlanningService planningService, CancellationToken ct) =>
{
    var plan = await planningService.GetPlanAsync(year, week, ct);
    if (plan is null)
    {
        return Results.NotFound();
    }

    var foodIds = plan.Meals.Select(m => m.FoodId).Distinct().ToList();
    var ingredients = await db.FoodIngredients
        .Where(fi => foodIds.Contains(fi.FoodId))
        .Include(fi => fi.Ingredient)
        .Select(fi => fi.Ingredient!)
        .Distinct()
        .OrderBy(i => i.Name)
        .ToListAsync(ct);

    var dto = new GroceryListDto(ingredients.Select(i => new IngredientDto(i.Id, i.Name)).ToList());
    return Results.Ok(dto);
});

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }));

app.MapFallbackToFile("index.html");

app.Run();

static bool IsValidMealType(MealType mealType) => mealType is MealType.Breakfast or MealType.Lunch or MealType.Dinner;

static WeeklyPlanDto ToDto(WeeklyPlan plan)
{
    var start = DateHelpers.GetWeekStartDate(plan.Year, plan.WeekNumber);
    var end = start.AddDays(6);
    return new WeeklyPlanDto(
        plan.Id,
        plan.Year,
        plan.WeekNumber,
        $"Week {plan.WeekNumber}, {plan.Year}",
        start.ToString("yyyy-MM-dd"),
        end.ToString("yyyy-MM-dd"),
        plan.Meals
            .OrderBy(m => m.DayOfWeek)
            .ThenBy(m => m.MealType)
            .Select(m => new MealEntryDto(
                m.Id,
                m.DayOfWeek,
                m.MealType,
                m.FoodId,
                m.Food?.Name ?? string.Empty,
                m.IsLeftover,
                m.LeftoverFromMealEntryId,
                m.ManualFoodId,
                m.BaseFoodId))
            .ToList());
}

static string NormalizeConnectionString(string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        throw new ArgumentException("Connection string is empty.");
    }

    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfoParts = (uri.UserInfo ?? string.Empty).Split(':', 2);
        var username = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : string.Empty;
        var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;

        var host = uri.Host;
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 is not null)
            {
                host = ipv4.ToString();
            }
        }
        catch
        {
            // ignore DNS failures; fall back to original host
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ToString();
    }

    return raw;
}
