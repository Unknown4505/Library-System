using System.Buffers.Binary;
using ImageMagick;

namespace BookKiosk.API.Services;

/// <summary>Decode untrusted covers before persistence; only server-encoded pixels are published.</summary>
public static class BookCoverImageValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const long MaxPixels = 16_000_000;

    public static async Task<byte[]?> NormalizeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaxBytes) return null;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var expected = extension switch
        {
            ".jpg" or ".jpeg" when string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) => "JPEG",
            ".png" when string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase) => "PNG",
            ".webp" when string.Equals(file.ContentType, "image/webp", StringComparison.OrdinalIgnoreCase) => "WEBP",
            _ => (string?)null
        };
        if (expected is null) return null;

        // Do not trust Length: also bound bytes actually read from the stream.
        using var input = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (input.Length + count > MaxBytes) return null;
                input.Write(buffer, 0, count);
            }
        }
        var bytes = input.ToArray();
        if (!HasSignature(bytes, expected)) return null;
        try
        {
            // Force the allowlisted decoder: never allow delegates or format auto-detection.
            var format = expected switch { "JPEG" => MagickFormat.Jpeg, "PNG" => MagickFormat.Png, _ => MagickFormat.WebP };
            var settings = new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = 1 };
            using var image = new MagickImage();
            var warning = false;
            image.Warning += (_, _) => warning = true;
            image.Ping(bytes, settings);
            if (warning || image.Width == 0 || image.Height == 0 || (long)image.Width * image.Height > MaxPixels) return null;
            image.Read(bytes, settings);
            if (warning) return null;
            cancellationToken.ThrowIfCancellationRequested();
            // Publish a static cover from decoded pixels, without client profiles or metadata.
            image.AutoOrient();
            image.Strip();
            image.Quality = 90;
            var output = image.ToByteArray(format);
            cancellationToken.ThrowIfCancellationRequested();
            return output.Length <= MaxBytes ? output : null;
        }
        catch (MagickException) { return null; }
    }

    private static bool HasSignature(byte[] bytes, string format) => format switch
    {
        "JPEG" => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff &&
            bytes[^2] == 0xff && bytes[^1] == 0xd9,
        "PNG" => bytes.Length >= 20 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) &&
            bytes.AsSpan(bytes.Length - 12).SequenceEqual(new byte[] {0,0,0,0,73,69,78,68,174,66,96,130}),
        "WEBP" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) == bytes.Length - 8,
        _ => false
    };
}
