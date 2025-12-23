namespace WeeklyDiet.Api.Models;

public class Food
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MealType AllowedMealTypes { get; set; }

    public ICollection<FoodIngredient> Ingredients { get; set; } = new List<FoodIngredient>();
    public ICollection<MealEntry> Meals { get; set; } = new List<MealEntry>();
}
