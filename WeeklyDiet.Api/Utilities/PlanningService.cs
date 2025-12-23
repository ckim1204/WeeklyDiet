using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WeeklyDiet.Api.Data;
using WeeklyDiet.Api.Models;

namespace WeeklyDiet.Api.Utilities;

public class PlanningService
{
    private readonly AppDbContext _dbContext;

    public PlanningService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WeeklyPlan?> GetPlanAsync(int year, int weekNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WeeklyPlans
            .Include(p => p.Meals)
            .ThenInclude(m => m.Food)
            .FirstOrDefaultAsync(p => p.Year == year && p.WeekNumber == weekNumber, cancellationToken);
    }

    public async Task<WeeklyPlan> GeneratePlanAsync(int year, int weekNumber, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.WeeklyPlans.AnyAsync(p => p.Year == year && p.WeekNumber == weekNumber, cancellationToken))
        {
            throw new InvalidOperationException($"Plan for {year}-W{weekNumber} already exists.");
        }

        var foods = await _dbContext.Foods
            .Include(f => f.Ingredients)
            .ToListAsync(cancellationToken);

        if (foods.Count == 0)
        {
            throw new InvalidOperationException("No foods available to generate a plan.");
        }

        var mealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
        foreach (var mt in mealTypes)
        {
            if (!foods.Any(f => f.AllowedMealTypes.HasFlag(mt)))
            {
                throw new InvalidOperationException($"Cannot generate plan: no foods are allowed for {mt}. Add at least one {mt.ToString().ToLower()} option.");
            }
        }
        var usage = new Dictionary<int, int>();
        var random = new Random(year * 100 + weekNumber);

        var plan = new WeeklyPlan
        {
            Year = year,
            WeekNumber = weekNumber
        };

        for (var day = 1; day <= 7; day++)
        {
            foreach (var mealType in mealTypes)
            {
                var candidates = foods.Where(f => f.AllowedMealTypes.HasFlag(mealType)).ToList();
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException($"No foods available for {mealType}.");
                }

                var food = SelectFood(candidates, usage, random);

                plan.Meals.Add(new MealEntry
                {
                    DayOfWeek = day,
                    MealType = mealType,
                    FoodId = food.Id,
                    BaseFoodId = food.Id,
                    ManualFoodId = null,
                    IsLeftover = false,
                    LeftoverFromMealEntryId = null
                });
            }
        }

        _dbContext.WeeklyPlans.Add(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPlanAsync(year, weekNumber, cancellationToken) ?? plan;
    }

    public async Task<MealEntry?> ReplaceMealAsync(int year, int weekNumber, int dayOfWeek, MealType mealType, int foodId, CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(year, weekNumber, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var entry = plan.Meals.FirstOrDefault(m => m.DayOfWeek == dayOfWeek && m.MealType == mealType);
        if (entry is null)
        {
            return null;
        }

        var food = await _dbContext.Foods.FirstOrDefaultAsync(f => f.Id == foodId, cancellationToken);
        if (food is null || !food.AllowedMealTypes.HasFlag(mealType))
        {
            return null;
        }

        entry.ManualFoodId = food.Id;
        entry.FoodId = food.Id;
        entry.IsLeftover = false;
        entry.LeftoverFromMealEntryId = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<(bool Success, string? Error)> ToggleLeftoverAsync(int year, int weekNumber, int dayOfWeek, MealType mealType, bool enable, CancellationToken cancellationToken = default)
    {
        var (currentYear, currentWeek) = DateHelpers.GetCurrentIsoWeek();
        if (currentYear != year || currentWeek != weekNumber)
        {
            return (false, "Leftovers can only be set on the current week.");
        }

        var sourcePlan = await GetPlanAsync(year, weekNumber, cancellationToken);
        if (sourcePlan is null)
        {
            return (false, "Plan not found.");
        }

        var sourceEntry = sourcePlan.Meals.FirstOrDefault(m => m.DayOfWeek == dayOfWeek && m.MealType == mealType);
        if (sourceEntry is null)
        {
            return (false, "Meal not found.");
        }

        var weekStart = DateHelpers.GetWeekStartDate(year, weekNumber);
        var sourceDate = weekStart.AddDays(dayOfWeek - 1);
        var targetDate = sourceDate.AddDays(1);

        var targetYear = ISOWeek.GetYear(targetDate);
        var targetWeek = ISOWeek.GetWeekOfYear(targetDate);
        var targetDay = DateHelpers.NormalizeDayOfWeek(targetDate);

        var targetPlan = await GetPlanAsync(targetYear, targetWeek, cancellationToken);
        if (targetPlan is null)
        {
            return (false, "Target week plan not found. Generate the plan first.");
        }
        var targetEntry = targetPlan.Meals.FirstOrDefault(m => m.DayOfWeek == targetDay && m.MealType == mealType);
        if (targetEntry is null)
        {
            return (false, "Target meal not found.");
        }

        if (enable)
        {
            ResetLeftover(targetEntry, true);
            targetEntry.IsLeftover = true;
            targetEntry.LeftoverFromMealEntryId = sourceEntry.Id;
            targetEntry.FoodId = sourceEntry.FoodId;
        }
        else
        {
            if (targetEntry.LeftoverFromMealEntryId == sourceEntry.Id)
            {
                ResetLeftover(targetEntry, true);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private static Food SelectFood(List<Food> candidates, Dictionary<int, int> usage, Random random)
    {
        var shuffled = candidates.OrderBy(_ => random.Next()).ToList();
        var unused = shuffled.FirstOrDefault(f => usage.GetValueOrDefault(f.Id, 0) == 0);
        if (unused is not null)
        {
            usage[unused.Id] = usage.GetValueOrDefault(unused.Id, 0) + 1;
            return unused;
        }

        var minUsage = shuffled.Min(f => usage.GetValueOrDefault(f.Id, 0));
        var options = shuffled.Where(f => usage.GetValueOrDefault(f.Id, 0) == minUsage).ToList();
        var selected = options[random.Next(options.Count)];
        usage[selected.Id] = usage.GetValueOrDefault(selected.Id, 0) + 1;
        return selected;
    }

    private static void ResetLeftover(MealEntry entry, bool resetFood)
    {
        entry.IsLeftover = false;
        entry.LeftoverFromMealEntryId = null;
        if (resetFood)
        {
            entry.FoodId = entry.ManualFoodId ?? entry.BaseFoodId ?? entry.FoodId;
        }
    }
}
