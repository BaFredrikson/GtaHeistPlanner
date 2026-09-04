using CommunityToolkit.Mvvm.ComponentModel;

namespace GtaHeistPlanner.App.ViewModels;

public partial class PatrolAssignmentViewModel : ViewModelBase
{
    public PatrolAssignmentViewModel(SecurityPatrolViewModel patrol, bool assigned)
    {
        Patrol = patrol;
        IsAssigned = assigned;
    }

    public SecurityPatrolViewModel Patrol { get; }
    public string Name => Patrol.Name;
    [ObservableProperty] public partial bool IsAssigned { get; set; }
}
