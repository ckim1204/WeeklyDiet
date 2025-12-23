namespace WeeklyDiet.Api.Models;

[Flags]
public enum MealType
{
    None = 0,
    Breakfast = 1,
    Lunch = 2,
    Dinner = 4
}
