using System.ComponentModel.DataAnnotations;

namespace WeeklyDiet.Api.DTOs;

public record IngredientDto(int Id, string Name);

public class IngredientCreateUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
