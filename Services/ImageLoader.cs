using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LabelAva.Services;

public static class ImageLoader
{
    public static Bitmap Load(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".tif" || ext == ".tiff")
            return FromTiff(filePath);
        return new Bitmap(filePath);
    }

    public static Bitmap LoadScaled(string filePath, int maxWidth, int maxHeight)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".tif" || ext == ".tiff")
            return FromTiffScaled(filePath, maxWidth, maxHeight);

        var bitmap = new Bitmap(filePath);
        if (bitmap.Size.Width <= maxWidth && bitmap.Size.Height <= maxHeight)
            return bitmap;

        double scaleX = (double)maxWidth / bitmap.Size.Width;
        double scaleY = (double)maxHeight / bitmap.Size.Height;
        double scale = Math.Min(scaleX, scaleY);

        int newWidth = (int)(bitmap.Size.Width * scale);
        int newHeight = (int)(bitmap.Size.Height * scale);

        var scaled = bitmap.CreateScaledBitmap(new PixelSize(newWidth, newHeight), BitmapInterpolationMode.HighQuality);
        bitmap.Dispose();
        return scaled;
    }

    private static WriteableBitmap FromTiff(string filePath)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
        return ImageSharpToWriteableBitmap(image);
    }

    private static WriteableBitmap FromTiffScaled(string filePath, int maxWidth, int maxHeight)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);

        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            double scaleX = (double)maxWidth / image.Width;
            double scaleY = (double)maxHeight / image.Height;
            double scale = Math.Min(scaleX, scaleY);
            int newW = (int)(image.Width * scale);
            int newH = (int)(image.Height * scale);
            image.Mutate(ctx => ctx.Resize(newW, newH));
        }

        return ImageSharpToWriteableBitmap(image);
    }

    private static WriteableBitmap ImageSharpToWriteableBitmap(Image<Rgba32> image)
    {
        var bmp = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888);

        using var fb = bmp.Lock();
        int h = image.Height;
        int w = image.Width;
        int rowBytes = fb.RowBytes;
        int pixelRowBytes = w * 4;

        unsafe
        {
            var destBase = (byte*)fb.Address.ToPointer();
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var src = accessor.GetRowSpan(y);
                    var dst = new Span<byte>(destBase + y * rowBytes, pixelRowBytes);
                    MemoryMarshal.AsBytes(src).CopyTo(dst);
                }
            });
        }

        return bmp;
    }
}
