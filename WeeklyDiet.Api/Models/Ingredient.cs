namespace WeeklyDiet.Api.Models;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<FoodIngredient> Foods { get; set; } = new List<FoodIngredient>();
}
