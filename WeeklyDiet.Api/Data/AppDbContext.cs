using Microsoft.EntityFrameworkCore;
using WeeklyDiet.Api.Models;

namespace WeeklyDiet.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<FoodIngredient> FoodIngredients => Set<FoodIngredient>();
    public DbSet<WeeklyPlan> WeeklyPlans => Set<WeeklyPlan>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ingredient>()
            .HasIndex(i => i.Name)
            .IsUnique();

        modelBuilder.Entity<Food>()
            .HasIndex(f => f.Name)
            .IsUnique();

        modelBuilder.Entity<WeeklyPlan>()
            .HasIndex(p => new { p.Year, p.WeekNumber })
            .IsUnique();

        modelBuilder.Entity<MealEntry>()
            .HasIndex(m => new { m.WeeklyPlanId, m.DayOfWeek, m.MealType })
            .IsUnique();

        modelBuilder.Entity<FoodIngredient>()
            .HasKey(fi => new { fi.FoodId, fi.IngredientId });

        modelBuilder.Entity<FoodIngredient>()
            .HasOne(fi => fi.Food)
            .WithMany(f => f.Ingredients)
            .HasForeignKey(fi => fi.FoodId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FoodIngredient>()
            .HasOne(fi => fi.Ingredient)
            .WithMany(i => i.Foods)
            .HasForeignKey(fi => fi.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MealEntry>()
            .HasOne(m => m.Food)
            .WithMany(f => f.Meals)
            .HasForeignKey(m => m.FoodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealEntry>()
            .HasOne(m => m.BaseFood)
            .WithMany()
            .HasForeignKey(m => m.BaseFoodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealEntry>()
            .HasOne(m => m.ManualFood)
            .WithMany()
            .HasForeignKey(m => m.ManualFoodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MealEntry>()
            .HasOne(m => m.LeftoverFromMealEntry)
            .WithMany()
            .HasForeignKey(m => m.LeftoverFromMealEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
