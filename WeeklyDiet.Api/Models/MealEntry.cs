namespace WeeklyDiet.Api.Models;

public class MealEntry
{
    public int Id { get; set; }

    public int WeeklyPlanId { get; set; }
    public WeeklyPlan? WeeklyPlan { get; set; }

    public int DayOfWeek { get; set; } // 1 = Monday, 7 = Sunday
    public MealType MealType { get; set; }

    public int FoodId { get; set; }
    public Food? Food { get; set; }

    public int? BaseFoodId { get; set; }
    public Food? BaseFood { get; set; }

    public int? ManualFoodId { get; set; }
    public Food? ManualFood { get; set; }

    public bool IsLeftover { get; set; }
    public int? LeftoverFromMealEntryId { get; set; }
    public MealEntry? LeftoverFromMealEntry { get; set; }
}
