using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class LootPlanningCategoryTests
{
    [Theory]
    [InlineData(LootType.CoquardJewelry, LootPlanningCategory.RingsJewelry, "Rings / Jewelry")]
    [InlineData(LootType.FertilityStatue, LootPlanningCategory.PremiumCase, "Premium Case")]
    [InlineData(LootType.VerticalDisplayGlassCase, LootPlanningCategory.VerticalDisplayCase, "Vertical Display Case")]
    [InlineData(LootType.Painting, LootPlanningCategory.Painting, "Painting")]
    [InlineData(LootType.LoadingBayCargo, LootPlanningCategory.LoadingBayCargo, "Loading Bay Cargo")]
    [InlineData(LootType.SafetyDepositBoxes, LootPlanningCategory.SafetyDepositBoxes, "Safety Deposit Boxes")]
    public void LootTypesMapToStablePlanningCategories(
        LootType type, LootPlanningCategory expected, string displayName)
    {
        var category = LootPlanningCategories.For(type);

        Assert.Equal(expected, category);
        Assert.Equal(displayName, category.DisplayName());
    }

    [Fact]
    public void BreakdownCombinesStandardAndCrispVariantsWithoutLosingMetadata()
    {
        var definitions = new[]
        {
            Loot("rings", "main-floor", "Coquard Rings & Bracelets", LootType.CoquardJewelry),
            Loot("crisp-rings", "upper-floor", "Coquard Rings", LootType.CoquardJewelry, LootZoneCatalog.CrispGalleryId),
            Loot("painting", "main-floor", "Painting", LootType.Painting),
            Loot("crisp-painting", "upper-floor", "Crisp Gallery Painting", LootType.Painting, LootZoneCatalog.CrispGalleryId),
            Loot("glass", "upper-floor", "Glass Case", LootType.VerticalDisplayGlassCase),
            Loot("crisp-glass", "upper-floor", "Vertical Display / Premium Case", LootType.VerticalDisplayGlassCase, LootZoneCatalog.CrispGalleryId),
            Loot("statue", "lower-floor", "Fertility Statue / Coquard Rings", LootType.FertilityStatue),
            Loot("crisp-statue", "upper-floor", "Fertility Statue / Premium Case", LootType.FertilityStatue, LootZoneCatalog.CrispGalleryId),
        };
        var states = definitions.Select(definition => new LootSpawnState
        {
            SpawnId = definition.Id,
            IsPresent = true,
        }).ToArray();

        var analysis = HeistPlanningAnalyzer.Analyze(definitions, states, 2);

        AssertCategory(analysis, LootPlanningCategory.RingsJewelry, "Rings / Jewelry", 2, 20);
        AssertCategory(analysis, LootPlanningCategory.Painting, "Painting", 2, 100);
        AssertCategory(analysis, LootPlanningCategory.VerticalDisplayCase, "Vertical Display Case", 2, 60);
        AssertCategory(analysis, LootPlanningCategory.PremiumCase, "Premium Case", 2, 40);
        Assert.DoesNotContain(analysis.Categories, category => category.Name.StartsWith("Fertility Statue", StringComparison.Ordinal));
        Assert.DoesNotContain(analysis.Categories, category => category.Name.StartsWith("Vertical Display /", StringComparison.Ordinal));
        Assert.DoesNotContain(analysis.Categories, category => category.Name.StartsWith("Crisp Gallery", StringComparison.Ordinal));

        Assert.Equal("Crisp Gallery Painting", definitions[3].Name);
        Assert.Equal("upper-floor", definitions[3].MapId);
        Assert.Equal(LootZoneCatalog.CrispGalleryId, definitions[3].ZoneId);
        Assert.Equal(2, analysis.Categories.Single(category => category.Category == LootPlanningCategory.Painting).AccessibleCount);
        Assert.Equal(OptionalPrep.GlassCutter,
            LootEconomicsCatalog.Get(definitions[5].Type, definitions[5].ZoneId).RequiredPrep);
    }

    [Fact]
    public void CanonicalGroupingDoesNotChangeAccessibilityEconomicsOrOptimizedHaul()
    {
        var definitions = new[]
        {
            Loot("normal-painting", "main-floor", "Painting", LootType.Painting),
            Loot("crisp-painting", "upper-floor", "Crisp Gallery Painting", LootType.Painting, LootZoneCatalog.CrispGalleryId),
            Loot("glass", "upper-floor", "Glass Case", LootType.VerticalDisplayGlassCase),
        };
        var states = new[]
        {
            State("normal-painting", 110_000),
            State("crisp-painting", 150_000),
            State("glass", 95_000),
        };

        var solo = HeistPlanningAnalyzer.Analyze(definitions, states, 1);
        var duo = HeistPlanningAnalyzer.Analyze(definitions, states, 2);

        Assert.Equal(1, solo.InaccessibleScopedCount);
        Assert.Equal(2, duo.Categories.Single(category => category.Category == LootPlanningCategory.Painting).AccessibleCount);
        Assert.Equal(["crisp-painting", "glass", "normal-painting"],
            duo.RecommendedHaul.SelectedLoot.Select(item => item.LootId).Order().ToArray());
        Assert.Equal(355_000, duo.RecommendedHaul.KnownExactSubtotal);
        Assert.Equal(130, duo.RecommendedHaul.BagUsagePercent);
        Assert.Equal(PrepRecommendationLevel.Required, duo.GlassCutter.Recommendation);
    }

    private static void AssertCategory(PlanningAnalysis analysis, LootPlanningCategory category,
        string name, int count, int bagPercent)
    {
        var summary = analysis.Categories.Single(item => item.Category == category);
        Assert.Equal(name, summary.Name);
        Assert.Equal(count, summary.TotalCount);
        Assert.Equal(bagPercent, summary.TotalBagPercent);
    }

    private static LootSpawnDefinition Loot(string id, string mapId, string name, LootType type,
        string? zoneId = null) => new(id, mapId, name, type, .5, .5, zoneId);

    private static LootSpawnState State(string id, int value) => new()
    {
        SpawnId = id,
        IsPresent = true,
        ScopedValue = value,
    };
}
