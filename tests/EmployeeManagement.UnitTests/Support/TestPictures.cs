using SkiaSharp;

namespace EmployeeManagement.UnitTests.Support;

public static class TestPictures
{
    public static byte[] Create(SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.SeaGreen);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }
}
