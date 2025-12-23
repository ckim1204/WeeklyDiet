using System.ComponentModel.DataAnnotations;
using WeeklyDiet.Api.Models;

namespace WeeklyDiet.Api.DTOs;

public record FoodDto(
    int Id,
    string Name,
    List<string> AllowedMealTypes,
    List<int> IngredientIds,
    List<IngredientDto> Ingredients);

public class FoodCreateUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<int> IngredientIds { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<MealType> AllowedMealTypes { get; set; } = new();
}
