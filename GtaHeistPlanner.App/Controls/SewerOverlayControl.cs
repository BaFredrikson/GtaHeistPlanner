using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.Controls;

public sealed class SewerOverlayControl : Control
{
    private static readonly Pen BranchPen = new(new SolidColorBrush(Color.Parse("#66788A9A")), 2);
    private static readonly Pen RoutePen = new(new SolidColorBrush(Color.Parse("#F3C969")), 5);
    private static readonly Pen SelectedPen = new(new SolidColorBrush(Color.Parse("#55D9FF")), 2);
    private static readonly IBrush NodeBrush = new SolidColorBrush(Color.Parse("#DCE7F2"));
    private static readonly IBrush StartBrush = new SolidColorBrush(Color.Parse("#52E36D"));
    private string? _draggedNodeId;

    public static readonly StyledProperty<IEnumerable<SewerNodeViewModel>?> NodesProperty =
        AvaloniaProperty.Register<SewerOverlayControl, IEnumerable<SewerNodeViewModel>?>(nameof(Nodes));
    public static readonly StyledProperty<IEnumerable<SewerConnection>?> ConnectionsProperty =
        AvaloniaProperty.Register<SewerOverlayControl, IEnumerable<SewerConnection>?>(nameof(Connections));
    public static readonly StyledProperty<IEnumerable<SewerConnection>?> HighlightedConnectionsProperty =
        AvaloniaProperty.Register<SewerOverlayControl, IEnumerable<SewerConnection>?>(nameof(HighlightedConnections));
    public static readonly StyledProperty<string?> StartNodeIdProperty =
        AvaloniaProperty.Register<SewerOverlayControl, string?>(nameof(StartNodeId));
    public static readonly StyledProperty<SewerNodeViewModel?> SelectedNodeProperty =
        AvaloniaProperty.Register<SewerOverlayControl, SewerNodeViewModel?>(nameof(SelectedNode));
    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<SewerOverlayControl, bool>(nameof(IsEditMode));
    public static readonly StyledProperty<double> MapAspectRatioProperty =
        AvaloniaProperty.Register<SewerOverlayControl, double>(nameof(MapAspectRatio), 1);
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<SewerOverlayControl, int>(nameof(Revision));
    public static readonly StyledProperty<ICommand?> AddNodeCommandProperty =
        AvaloniaProperty.Register<SewerOverlayControl, ICommand?>(nameof(AddNodeCommand));
    public static readonly StyledProperty<ICommand?> MoveNodeCommandProperty =
        AvaloniaProperty.Register<SewerOverlayControl, ICommand?>(nameof(MoveNodeCommand));
    public static readonly StyledProperty<ICommand?> SelectNodeCommandProperty =
        AvaloniaProperty.Register<SewerOverlayControl, ICommand?>(nameof(SelectNodeCommand));

    static SewerOverlayControl() => AffectsRender<SewerOverlayControl>(
        NodesProperty, ConnectionsProperty, HighlightedConnectionsProperty, StartNodeIdProperty,
        SelectedNodeProperty, IsEditModeProperty, MapAspectRatioProperty, RevisionProperty);

    public IEnumerable<SewerNodeViewModel>? Nodes { get => GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IEnumerable<SewerConnection>? Connections { get => GetValue(ConnectionsProperty); set => SetValue(ConnectionsProperty, value); }
    public IEnumerable<SewerConnection>? HighlightedConnections { get => GetValue(HighlightedConnectionsProperty); set => SetValue(HighlightedConnectionsProperty, value); }
    public string? StartNodeId { get => GetValue(StartNodeIdProperty); set => SetValue(StartNodeIdProperty, value); }
    public SewerNodeViewModel? SelectedNode { get => GetValue(SelectedNodeProperty); set => SetValue(SelectedNodeProperty, value); }
    public bool IsEditMode { get => GetValue(IsEditModeProperty); set => SetValue(IsEditModeProperty, value); }
    public double MapAspectRatio { get => GetValue(MapAspectRatioProperty); set => SetValue(MapAspectRatioProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public ICommand? AddNodeCommand { get => GetValue(AddNodeCommandProperty); set => SetValue(AddNodeCommandProperty, value); }
    public ICommand? MoveNodeCommand { get => GetValue(MoveNodeCommandProperty); set => SetValue(MoveNodeCommandProperty, value); }
    public ICommand? SelectNodeCommand { get => GetValue(SelectNodeCommandProperty); set => SetValue(SelectNodeCommandProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var nodes = Nodes?.ToDictionary(node => node.Id, StringComparer.Ordinal) ?? [];
        var rect = GetMapRect();
        foreach (var connection in Connections ?? [])
            if (nodes.TryGetValue(connection.FromNodeId, out var from) && nodes.TryGetValue(connection.ToNodeId, out var to))
                context.DrawLine(BranchPen, ToScreen(from, rect), ToScreen(to, rect));
        foreach (var connection in HighlightedConnections ?? [])
            if (nodes.TryGetValue(connection.FromNodeId, out var from) && nodes.TryGetValue(connection.ToNodeId, out var to))
                context.DrawLine(RoutePen, ToScreen(from, rect), ToScreen(to, rect));
        foreach (var node in nodes.Values)
        {
            var point = ToScreen(node, rect);
            context.DrawEllipse(node.Id == StartNodeId ? StartBrush : NodeBrush, node == SelectedNode ? SelectedPen : null, point, 6, 6);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetPosition(this);
        var node = FindNode(point);
        if (node is not null)
        {
            SelectNodeCommand?.Execute(node.Id);
            if (IsEditMode) { _draggedNodeId = node.Id; e.Pointer.Capture(this); }
            e.Handled = true;
        }
        else if (IsEditMode && TryNormalize(point, out var normalized))
        {
            AddNodeCommand?.Execute(normalized);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_draggedNodeId is not null && TryNormalize(e.GetPosition(this), out var point))
            MoveNodeCommand?.Execute(new SewerNodeMove(_draggedNodeId, point.X, point.Y));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _draggedNodeId = null;
        e.Pointer.Capture(null);
    }

    private SewerNodeViewModel? FindNode(Point point)
    {
        var rect = GetMapRect();
        return Nodes?.OrderBy(node => Distance(point, ToScreen(node, rect))).FirstOrDefault(node => Distance(point, ToScreen(node, rect)) <= 12);
    }

    private Rect GetMapRect()
    {
        var width = Math.Min(Bounds.Width, Bounds.Height * MapAspectRatio);
        var height = width / MapAspectRatio;
        return new((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    private static Point ToScreen(SewerNodeViewModel node, Rect rect) =>
        new(rect.X + node.MapX * rect.Width, rect.Y + node.MapY * rect.Height);

    private bool TryNormalize(Point point, out MapPoint normalized)
    {
        var rect = GetMapRect();
        if (!rect.Contains(point)) { normalized = default; return false; }
        normalized = new((point.X - rect.X) / rect.Width, (point.Y - rect.Y) / rect.Height);
        return true;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
