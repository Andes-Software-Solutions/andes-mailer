using System.Text;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Smallest byte sequences that carry each accepted type's signature.
public static class TestFiles
{
    public static byte[] Pdf { get; } = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n");

    public static byte[] Zip { get; } = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];

    public static byte[] Png { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    public static byte[] Jpeg { get; } = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    public static byte[] Gif { get; } = Encoding.ASCII.GetBytes("GIF89a\u0001\u0000");

    public static byte[] Webp { get; } = Encoding.ASCII.GetBytes("RIFF$\u0000\u0000\u0000WEBPVP8 ");

    public static byte[] Executable { get; } = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];
}
