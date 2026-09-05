using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class StageViewPolicyTests
{
    [Fact]
    public void PreparationShowsEveryLootNodeClearly()
    {
        var policy = StageViewPolicies.Get(PlannerStage.Preparation);

        Assert.Equal(LootVisibilityMode.AllClearly, policy.LootVisibility);
        Assert.True(policy.AllowsOverlay(OverlayType.Loot, "main-floor"));
    }

    [Fact]
    public void InfiltrationAllowsOnlyExteriorMapsAndOverlays()
    {
        var policy = StageViewPolicies.Get(PlannerStage.HeistInfiltration);

        Assert.True(policy.AllowsMap("exterior-firstfloor"));
        Assert.False(policy.AllowsMap("main-floor"));
        Assert.True(policy.AllowsOverlay(OverlayType.EntryPoints, "exterior-firstfloor"));
        Assert.True(policy.AllowsOverlay(OverlayType.ExteriorGuards, "exterior-firstfloor"));
        Assert.True(policy.AllowsOverlay(OverlayType.ExteriorCameras, "exterior-firstfloor"));
        Assert.False(policy.AllowsOverlay(OverlayType.Loot, "exterior-firstfloor"));
        Assert.False(policy.AllowsOverlay(OverlayType.ExteriorCameras, "main-floor"));
    }

    [Fact]
    public void ActivityAllowsFourInternalMapsWithoutSewerAndInternalOverlays()
    {
        var policy = StageViewPolicies.Get(PlannerStage.HeistActivity);

        Assert.Equal(4, policy.AllowedMapIds.Count);
        Assert.Contains("main-floor", policy.AllowedMapIds);
        Assert.Contains("upper-floor", policy.AllowedMapIds);
        Assert.Contains("lower-floor", policy.AllowedMapIds);
        Assert.Contains("basement", policy.AllowedMapIds);
        Assert.DoesNotContain("sewer", policy.AllowedMapIds);
        Assert.DoesNotContain("exterior-firstfloor", policy.AllowedMapIds);
        Assert.True(policy.AllowsOverlay(OverlayType.InteriorGuards, "main-floor"));
        Assert.True(policy.AllowsOverlay(OverlayType.InteriorCameras, "upper-floor"));
        Assert.False(policy.AllowsOverlay(OverlayType.EntryPoints, "main-floor"));
    }

    [Theory]
    [InlineData(PlannerStage.Preparation)]
    [InlineData(PlannerStage.Planning)]
    [InlineData(PlannerStage.HeistInfiltration)]
    [InlineData(PlannerStage.HeistActivity)]
    public void ExteriorMazeIsExcludedFromOrdinaryNavigation(PlannerStage stage) =>
        Assert.DoesNotContain("exterior-maze", StageViewPolicies.Get(stage).AllowedMapIds);

    [Fact]
    public void OverlayRequiresBothStagePermissionAndMatchingAllowedMap()
    {
        var policy = StageViewPolicies.Get(PlannerStage.HeistInfiltration);

        Assert.True(policy.AllowsOverlay(OverlayType.ExteriorCameras, "exterior-rooftop"));
        Assert.False(policy.AllowsOverlay(OverlayType.ExteriorCameras, "upper-floor"));
        Assert.False(policy.AllowsOverlay(OverlayType.InteriorCameras, "exterior-rooftop"));
    }

    [Fact]
    public void RedundantExteriorGroundFloorIsExcludedFromAllNormalStages()
    {
        foreach (var stage in Enum.GetValues<PlannerStage>())
            Assert.DoesNotContain("exterior-groundfloor", StageViewPolicies.Get(stage).AllowedMapIds);
    }

    [Fact]
    public void InfiltrationReturnsAllRelevantMapsForSimultaneousDisplay()
    {
        var maps = StageViewPolicies.Get(PlannerStage.HeistInfiltration).AllowedMapIds;

        Assert.Equal(4, maps.Count);
        Assert.Contains("exterior-firstfloor", maps);
        Assert.Contains("exterior-second-floor", maps);
        Assert.Contains("exterior-rooftop", maps);
        Assert.Contains("sewer", maps);
    }

    [Fact]
    public void SewerBelongsOnlyToInfiltration()
    {
        Assert.Contains("sewer", StageViewPolicies.Get(PlannerStage.HeistInfiltration).AllowedMapIds);
        Assert.DoesNotContain("sewer", StageViewPolicies.Get(PlannerStage.HeistActivity).AllowedMapIds);
    }

    [Fact]
    public void DeveloperModeIsRequiredForLootAuthoring()
    {
        var preparation = StageViewPolicies.Get(PlannerStage.Preparation);
        var infiltration = StageViewPolicies.Get(PlannerStage.HeistInfiltration);

        Assert.False(DeveloperViewPolicy.CanAuthorLoot(false, preparation, "main-floor"));
        Assert.True(DeveloperViewPolicy.CanAuthorLoot(true, preparation, "main-floor"));
        Assert.False(DeveloperViewPolicy.CanAuthorLoot(true, infiltration, "exterior-firstfloor"));
    }

    [Fact]
    public void DeveloperModeAndOperationalStageAreRequiredForSecurityAuthoring()
    {
        Assert.False(DeveloperViewPolicy.CanAuthorSecurity(false, PlannerStage.HeistActivity));
        Assert.False(DeveloperViewPolicy.CanAuthorSecurity(true, PlannerStage.Preparation));
        Assert.True(DeveloperViewPolicy.CanAuthorSecurity(true, PlannerStage.HeistInfiltration));
        Assert.True(DeveloperViewPolicy.CanAuthorSecurity(true, PlannerStage.HeistActivity));
    }
}
