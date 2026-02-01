using System.Text;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// Thin wrappers around BinaryReader/BinaryWriter that centralize R4 quirks:
/// - null-terminated metadata strings
/// - the "align to 4, but if already aligned jump 4 more" seek behavior
/// </summary>
internal static class R4Binary
{
    /// <summary>
    /// Matches your serializer's alignment logic:
    /// Seek((pos + 3) & ~3) + (pos % 4 == 0 ? 4 : 0)
    /// This is important because it relies on FileStream zero-fill when seeking forward.
    /// </summary>
    public static void SeekAlign4WithExtraBlock(Stream stream)
    {
        var pos = stream.Position;
        var aligned = (pos + 3) & ~3;
        if (pos % 4 == 0) aligned += 4;
        stream.Seek(aligned, SeekOrigin.Begin);
    }

    /// <summary>
    /// Lifted from your R4GameDeserializer.ReadItemMeta; behavior preserved.
    /// </summary>
    public static (string Title, string? Description) ReadItemMeta(BinaryReader reader, bool game = false)
    {
        var titleBytes = new List<byte>();
        byte bx;
        while ((bx = reader.ReadByte()) != 0)
            titleBytes.Add(bx);

        if (game)
        {
            reader.BaseStream.Seek((4 - reader.BaseStream.Position % 4) % 4, SeekOrigin.Current);
            return (Encoding.ASCII.GetString(titleBytes.ToArray()), null);
        }

        var descriptionBytes = new List<byte>();
        byte by;
        while ((by = reader.ReadByte()) != 0)
            descriptionBytes.Add(by);

        reader.BaseStream.Seek((4 - reader.BaseStream.Position % 4) % 4, SeekOrigin.Current);

        return (Encoding.ASCII.GetString(titleBytes.ToArray()),
            Encoding.ASCII.GetString(descriptionBytes.ToArray()));
    }

    /// <summary>
    /// Lifted from your R4GameDeserializer.ReadCheatCodes; behavior preserved.
    /// </summary>
    public static List<byte[]> ReadCheatCodes(BinaryReader reader, ulong numChunks)
    {
        if (numChunks == 0) return [];

        var cheatCodes = new List<byte[]>((int)Math.Min(numChunks, 1024));
        for (ulong i = 0; i < numChunks; i++)
        {
            var cheatCode = reader.ReadBytes(4);
            if (cheatCode.Length < 4)
                throw new InvalidDataException("Incomplete cheat code found.");
            cheatCodes.Add(cheatCode);
        }

        return cheatCodes;
    }

    /// <summary>
    /// Matches your writer behavior:
    /// - title chars (no explicit trailing 0)
    /// - 0x00 separator
    /// - description chars (no explicit trailing 0)
    /// - then seek forward, letting the stream zero-fill produce terminators/padding.
    /// </summary>
    public static void WriteTitleAndDesc(BinaryWriter writer, string title, string description)
    {
        writer.Write(title.ToCharArray());
        writer.Write((byte)0x00);
        writer.Write(description.ToCharArray());
        SeekAlign4WithExtraBlock(writer.BaseStream);
    }
}