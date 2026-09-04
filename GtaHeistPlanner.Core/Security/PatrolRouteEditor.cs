namespace GtaHeistPlanner.Core.Security;

public static class PatrolRouteEditor
{
    public static IReadOnlyList<PatrolWaypoint> Move(
        IReadOnlyList<PatrolWaypoint> waypoints, int index, PatrolWaypoint position)
    {
        ValidateIndex(waypoints, index);
        var result = waypoints.ToList();
        result[index] = position;
        return result;
    }

    public static IReadOnlyList<PatrolWaypoint> Delete(IReadOnlyList<PatrolWaypoint> waypoints, int index)
    {
        ValidateIndex(waypoints, index);
        var result = waypoints.ToList();
        result.RemoveAt(index);
        return result;
    }

    private static void ValidateIndex(IReadOnlyList<PatrolWaypoint> waypoints, int index)
    {
        if (index < 0 || index >= waypoints.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
