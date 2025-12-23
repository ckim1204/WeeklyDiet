using WeeklyDiet.Api.Models;

namespace WeeklyDiet.Api.DTOs;

public record MealEntryDto(
    int Id,
    int DayOfWeek,
    MealType MealType,
    int FoodId,
    string FoodName,
    bool IsLeftover,
    int? LeftoverFromMealEntryId,
    int? ManualFoodId,
    int? BaseFoodId);

public record WeeklyPlanDto(
    int Id,
    int Year,
    int WeekNumber,
    string WeekLabel,
    string StartDate,
    string EndDate,
    List<MealEntryDto> Meals);

public record GroceryListDto(List<IngredientDto> Ingredients);
