using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Security;

public static class PatrolAnimation
{
    public const double DefaultSpeed = 0.10;
    public const double NodeWaitSeconds = 0.5;

    public static MapPoint PositionAt(
        IReadOnlyList<PatrolWaypoint> waypoints,
        double elapsedSeconds,
        double speed = DefaultSpeed)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        if (waypoints.Count == 0)
            throw new ArgumentException("A patrol must contain at least one waypoint.", nameof(waypoints));
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (waypoints.Count == 1)
            return new MapPoint(waypoints[0].X, waypoints[0].Y);

        var legs = BuildRoundTrip(waypoints, speed);
        var cycleDuration = legs.Sum(leg => leg.Duration) + legs.Count * NodeWaitSeconds;
        var time = elapsedSeconds % cycleDuration;

        if (time < NodeWaitSeconds)
            return new MapPoint(waypoints[0].X, waypoints[0].Y);
        time -= NodeWaitSeconds;

        for (var index = 0; index < legs.Count; index++)
        {
            var leg = legs[index];
            if (time < leg.Duration)
            {
                var progress = leg.Duration <= 0 ? 1 : time / leg.Duration;
                return new MapPoint(
                    leg.From.X + (leg.To.X - leg.From.X) * progress,
                    leg.From.Y + (leg.To.Y - leg.From.Y) * progress);
            }

            time -= leg.Duration;
            if (index == legs.Count - 1)
                break;
            if (time < NodeWaitSeconds)
                return new MapPoint(leg.To.X, leg.To.Y);
            time -= NodeWaitSeconds;
        }

        return new MapPoint(waypoints[0].X, waypoints[0].Y);
    }

    private static IReadOnlyList<Leg> BuildRoundTrip(IReadOnlyList<PatrolWaypoint> waypoints, double speed)
    {
        var legs = new List<Leg>((waypoints.Count - 1) * 2);
        for (var index = 1; index < waypoints.Count; index++)
            legs.Add(CreateLeg(waypoints[index - 1], waypoints[index], speed));
        for (var index = waypoints.Count - 1; index > 0; index--)
            legs.Add(CreateLeg(waypoints[index], waypoints[index - 1], speed));
        return legs;
    }

    private static Leg CreateLeg(PatrolWaypoint from, PatrolWaypoint to, double speed)
    {
        var distance = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        return new Leg(from, to, distance / speed);
    }

    private sealed record Leg(PatrolWaypoint From, PatrolWaypoint To, double Duration);
}
