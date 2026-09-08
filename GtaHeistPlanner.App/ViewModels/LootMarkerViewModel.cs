using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.App.ViewModels;

public partial class LootMarkerViewModel : ViewModelBase
{
    private readonly LootSpawnState _state;
    public string Id { get; }
    public string MapId { get; }
    public IReadOnlyList<string> VoiceAliases { get; }
    public string LabelText => Name;

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial LootType Type { get; set; }
    [ObservableProperty] public partial double X { get; set; }
    [ObservableProperty] public partial double Y { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrispGallery))]
    [NotifyPropertyChangedFor(nameof(Economics))]
    [NotifyPropertyChangedFor(nameof(Zone))]
    public partial string? ZoneId { get; set; }
    [ObservableProperty] public partial bool IsPresent { get; set; }
    [ObservableProperty] public partial bool IsBuyersRequest { get; set; }
    [ObservableProperty] public partial bool IsLooted { get; set; }
    [ObservableProperty] public partial int? ScopedValue { get; set; }

    public LootEconomics Economics => LootEconomicsCatalog.Get(Type, ZoneId);
    public LootEconomics SelectedEconomics
    {
        get => LootEconomicsCatalog.Standard.Single(item => item.Type == Type);
        set => Type = value.Type;
    }
    public LootZoneDefinition? Zone => LootZoneCatalog.Get(ZoneId);
    public string EstimatedValueDisplay => $"${Economics.MinValue:N0} – ${Economics.MaxValue:N0}";
    public string PrepDisplay => Economics.RequiredPrep switch
    {
        OptionalPrep.None => "None",
        OptionalPrep.GlassCutter => "Glass Cutter",
        OptionalPrep.PowerDrills => "Power Drills",
        _ => Economics.RequiredPrep.ToString(),
    };
    public string AccessDisplay => Zone is null
        ? "Minimum players: 1"
        : $"{Zone.DisplayName} · minimum players: {Zone.MinimumPlayers}";
    public bool IsCrispGallery
    {
        get => ZoneId == LootZoneCatalog.CrispGalleryId;
        set => ZoneId = value ? LootZoneCatalog.CrispGalleryId : null;
    }

    public LootMarkerViewModel(LootSpawnDefinition definition, LootSpawnState state)
    {
        _state = state;
        Id = definition.Id;
        MapId = definition.MapId;
        VoiceAliases = definition.VoiceAliases;
        Name = definition.Name;
        Type = definition.Type;
        X = definition.X;
        Y = definition.Y;
        ZoneId = definition.ZoneId;
        ApplyState(state);
    }

    public LootSpawnDefinition ToDefinition() => new(Id, MapId, Name, Type, X, Y, ZoneId)
    {
        VoiceAliases = VoiceAliases,
    };

    public void ApplyState(LootSpawnState state)
    {
        IsPresent = state.IsPresent;
        IsBuyersRequest = state.IsBuyersRequest;
        IsLooted = state.IsLooted;
        ScopedValue = state.ScopedValue;
    }

    partial void OnTypeChanged(LootType value)
    {
        OnPropertyChanged(nameof(Economics));
        OnPropertyChanged(nameof(SelectedEconomics));
        OnPropertyChanged(nameof(EstimatedValueDisplay));
        OnPropertyChanged(nameof(PrepDisplay));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(LabelText));

    partial void OnZoneIdChanged(string? value)
    {
        OnPropertyChanged(nameof(AccessDisplay));
        OnPropertyChanged(nameof(EstimatedValueDisplay));
        OnPropertyChanged(nameof(PrepDisplay));
    }
    partial void OnScopedValueChanged(int? value)
    {
        _state.ScopedValue = value;
        if (value is null)
            return;
        _state.IsPresent = true;
        IsPresent = true;
    }
}
