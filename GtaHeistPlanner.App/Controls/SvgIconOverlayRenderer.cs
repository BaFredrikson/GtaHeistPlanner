using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace GtaHeistPlanner.App.Controls;

internal sealed class SvgIconOverlayRenderer
{
    private readonly IReadOnlyList<SvgPath> _paths;
    private readonly double _displaySize;
    private readonly double _sourceSize;

    public SvgIconOverlayRenderer(Uri assetUri, double displaySize, double sourceSize, Color tintColor)
    {
        _displaySize = displaySize;
        _sourceSize = sourceSize;
        _paths = LoadPaths(assetUri, tintColor);
    }

    public void Draw(DrawingContext context, Point center, double visualScale = 1)
    {
        var scale = _displaySize / _sourceSize * visualScale;
        using var transform = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y));

        foreach (var path in _paths)
        {
            context.DrawGeometry(path.Fill, path.Stroke, path.Geometry);
        }
    }

    private static IReadOnlyList<SvgPath> LoadPaths(Uri assetUri, Color tintColor)
    {
        using var stream = AssetLoader.Open(assetUri);
        var document = XDocument.Load(stream);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "path")
            .Select(element =>
            {
                var geometry = Geometry.Parse((string?)element.Attribute("d")
                    ?? throw new InvalidDataException($"SVG path in '{assetUri}' has no path data."));
                var fillValue = (string?)element.Attribute("fill");
                var strokeValue = (string?)element.Attribute("stroke");
                IBrush? fill = fillValue is null or "none" ? null : new SolidColorBrush(tintColor);
                Pen? stroke = strokeValue is null or "none"
                    ? null
                    : new Pen(new SolidColorBrush(Color.Parse(strokeValue)),
                        double.Parse((string?)element.Attribute("stroke-width") ?? "1", CultureInfo.InvariantCulture));
                return new SvgPath(geometry, fill, stroke);
            })
            .ToArray();
    }

    private sealed record SvgPath(Geometry Geometry, IBrush? Fill, Pen? Stroke);
}
