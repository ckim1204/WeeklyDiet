namespace WeeklyDiet.Api.Models;

public class WeeklyPlan
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int WeekNumber { get; set; }

    public ICollection<MealEntry> Meals { get; set; } = new List<MealEntry>();
}
