using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace GtaHeistPlanner.App.Controls;

internal sealed class IconOverlayRenderer(
    Uri assetUri,
    double displaySize,
    Rect? sourceBounds = null,
    Color? tintColor = null)
{
    private readonly Lazy<Bitmap> _bitmap = new(() => LoadBitmap(assetUri, tintColor));

    public void Draw(DrawingContext context, Point center, double rotationDegrees = 0, double opacity = 1)
    {
        var bitmap = _bitmap.Value;
        var source = sourceBounds ?? new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var aspectRatio = source.Width / source.Height;
        var width = aspectRatio <= 1 ? displaySize * aspectRatio : displaySize;
        var height = aspectRatio <= 1 ? displaySize : displaySize / aspectRatio;
        var destination = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
        using var transform = context.PushTransform(
            Matrix.CreateRotation(rotationDegrees * Math.PI / 180, center));
        using var opacityScope = context.PushOpacity(opacity);
        context.DrawImage(bitmap, source, destination);
    }

    private static Bitmap LoadBitmap(Uri assetUri, Color? tintColor)
    {
        using var stream = AssetLoader.Open(assetUri);
        var source = new Bitmap(stream);
        if (tintColor is null)
        {
            return source;
        }

        var tinted = new WriteableBitmap(
            source.PixelSize,
            source.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = tinted.Lock())
        {
            source.CopyPixels(framebuffer);
            var pixels = new byte[framebuffer.RowBytes * framebuffer.Size.Height];
            System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

            for (var y = 0; y < framebuffer.Size.Height; y++)
            {
                for (var x = 0; x < framebuffer.Size.Width; x++)
                {
                    var offset = y * framebuffer.RowBytes + x * 4;
                    var alpha = pixels[offset + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    // Colorize the complete grayscale icon so antialiased edge pixels
                    // transition from black to the tint instead of leaving a white fringe.
                    var brightness = Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));
                    pixels[offset] = (byte)(brightness * tintColor.Value.B / 255);
                    pixels[offset + 1] = (byte)(brightness * tintColor.Value.G / 255);
                    pixels[offset + 2] = (byte)(brightness * tintColor.Value.R / 255);
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        source.Dispose();
        return tinted;
    }
}
