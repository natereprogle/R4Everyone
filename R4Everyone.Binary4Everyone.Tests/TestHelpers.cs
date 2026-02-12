using System.Text;

namespace R4Everyone.Binary4Everyone.Tests;

internal static class TestHelpers
{
    public static byte[] BuildAsciiMetaBytes(string title, string? description = null, bool game = false)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes(title));
        writer.Write((byte)0x00);

        if (!game)
        {
            writer.Write(Encoding.ASCII.GetBytes(description ?? string.Empty));
            writer.Write((byte)0x00);
        }

        var padding = (4 - (ms.Position % 4)) % 4;
        if (padding > 0)
        {
            writer.Write(new byte[padding]);
        }

        writer.Flush();
        return ms.ToArray();
    }

    public static R4Game CreateGame(string gameId = "ABCD")
    {
        return new R4Game(gameId)
        {
            GameTitle = "T",
            GameEnabled = true,
            GameChecksum = "89ABCDEF",
            MasterCodes =
            [
                0x11111111, 0x22222222, 0x33333333, 0x44444444,
                0x55555555, 0x66666666, 0x77777777, 0x88888888
            ]
        };
    }

    public static R4Cheat CreateCheat(bool enabled = true)
    {
        return new R4Cheat
        {
            Title = "C",
            Description = string.Empty,
            Enabled = enabled,
            Code = [BitConverter.GetBytes(0x12345678u), BitConverter.GetBytes(0x9ABCDEF0u)]
        };
    }

    public static byte[] SerializeGame(R4Game game)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        var codec = new R4GameCodec();
        codec.WriteGame(writer, game);
        writer.Flush();
        return ms.ToArray();
    }

    public static R4Database CreateDatabaseWithOneGame()
    {
        var db = new R4Database
        {
            Title = "UnitTest DB",
            FileEncoding = R4Encoding.UTF8,
            Enabled = true
        };
        db.Games.Add(CreateGame());
        db.Games[0].Items.Add(CreateCheat());
        return db;
    }

    public static byte[] SaveDatabaseToBytes(R4Database db)
    {
        using var ms = new MemoryStream();
        var codec = new R4DatabaseCodec();
        codec.SaveAsync(db, ms).GetAwaiter().GetResult();
        return ms.ToArray();
    }
}
