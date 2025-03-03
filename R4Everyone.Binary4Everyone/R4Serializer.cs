using System.Text;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// Handles serializing an R4Database back into binary. This class is a little complex, so the logic lives here rather than the R4Database file
/// </summary>
public class R4Serializer
{
    /// <summary>
    /// Creates an R4Serializer, overriding the FilePath in the R4Database
    /// </summary>
    /// <param name="filePath">The path to write the cheat database to</param>
    /// <param name="r4Database">The database to serialize</param>
    public R4Serializer(string filePath, R4Database r4Database) : this(r4Database)
    {
        if (!Path.Exists(filePath) || string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("The file path does not exist");
        }

        r4Database.R4FilePath = filePath;
        _r4Database = r4Database;
    }

    /// <summary>
    /// Creates an R4Serializer using the file path already in the file
    /// </summary>
    /// <param name="r4Database">The R4Database to serialize</param>
    public R4Serializer(R4Database r4Database)
    {
        if (!Path.Exists(r4Database.R4FilePath) || string.IsNullOrWhiteSpace(r4Database.R4FilePath))
        {
            throw new ArgumentException("The file path does not exist");
        }

        _r4Database = r4Database;
    }

    private readonly R4Database _r4Database;

    /// <summary>
    /// Serializes the R4Database into binary and writes it to the file path specified in the constructor
    /// </summary>
    /// <exception cref="IOException">Thrown if the file could not be written to</exception>
    public async Task SerializeAsync()
    {
        // It's not possible for this to be null since we guarantee it's not in the constructor(s)
        await using var writer = new BinaryWriter(File.OpenWrite(_r4Database.R4FilePath!));

        // Write the header
        writer.Write("R4 CheatCode"u8.ToArray());
        writer.Write((short)256);

        // Write the title
        writer.Seek(0x10, SeekOrigin.Begin);
        writer.Write(_r4Database.Title.ToCharArray());

        // Write the encoding
        writer.Seek(0x4C, SeekOrigin.Begin);
        writer.Write(R4EncodingHelper.GetBytes(_r4Database.FileEncoding));

        // Write the enabled flag
        writer.Write(_r4Database.Enabled ? (byte)0x01 : (byte)0x00);

        // Write the game addresses
        writer.Seek(0x0100, SeekOrigin.Begin);
        var gameOffsetPairs = CalculateGameAddresses();
        foreach (var (game, offset) in gameOffsetPairs)
        {
            // Write the 4-character Game ID (padded/truncated to 4 bytes)
            var gameIdBytes = Encoding.ASCII.GetBytes(game.GameId.PadRight(4)[..4]);
            writer.Write(gameIdBytes);

            // Write the 4-byte Checksum. The checksum is represented as text and is written VERBATIM to the file in little-endian
            // I.e. if the checksum was 2F00B549, it would be written to the file as 0x49 0xB5 0x00 0x2F and displayed as 2F00B549
            var checksumBytes = Enumerable.Range(0, 8)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(game.GameChecksum.Substring(x, 2), 16))
                .ToArray();

            // Reverse for little-endian order and write
            Array.Reverse(checksumBytes);
            writer.Write(checksumBytes);

            // Write the 4-byte Offset
            writer.Write(offset);

            // Calculate padding needed to align to the next 16-byte boundary
            var bytesWritten = gameIdBytes.Length + checksumBytes.Length + sizeof(uint);
            var padding = 16 - bytesWritten;

            // Write padding bytes if necessary
            if (padding > 0)
            {
                writer.Write(new byte[padding]);
            }
        }

        writer.Write(new byte[16]);

        // Write the games
        foreach (var game in _r4Database.Games)
        {
            await SerializeGameAsync(writer, game);
        }

        writer.Close();
    }

    private static async Task SerializeGameAsync(BinaryWriter writer, R4Game game)
    {
        // Write the title
        writer.Write(game.GameTitle.ToCharArray());

        // Seek to the next 4-byte border 
        writer.Seek((int)((writer.BaseStream.Position + 3) & ~3) + (writer.BaseStream.Position % 4 == 0 ? 4 : 0),
            SeekOrigin.Begin);

        // Write the number of items, written to the next two bytes
        writer.Write((ushort)Math.Min(game.ItemCount, ushort.MaxValue));

        // Write 0x00 0xF0 if enabled, and 0x00 0x00 if disabled
        writer.Write(game.GameEnabled ? (ushort)0xF000 : (ushort)0x0000);

        // Write the master codes
        foreach (var masterCode in game.MasterCodes)
        {
            writer.Write(masterCode);
        }

        // Write the cheats
        foreach (var item in game.Items)
        {
            switch (item)
            {
                case R4Cheat cheat:
                    await SerializeCheatAsync(writer, cheat);
                    break;
                case R4Folder folder:
                    await SerializeFolderAsync(writer, folder);
                    break;
            }
        }
    }

    private static Task SerializeCheatAsync(BinaryWriter writer, R4Cheat cheat)
    {
        // Write the number of 4-byte chunks the cheat takes up. 
        // We need to round up, so we add 3 to simulate the division ceiling
        writer.Write((ushort)Math.Min((cheat.Size - 1) / 4, ushort.MaxValue));

        // Write the enable flag
        writer.Write(cheat.Enabled ? (ushort)0x0100 : (ushort)0x0000);

        // Write the title
        writer.Write(cheat.Title.ToCharArray());

        // Write the separator byte
        writer.Write((byte)0x00);

        // Write the description
        writer.Write(cheat.Description.ToCharArray());

        // Seek to the next 4-byte border
        writer.Seek((int)((writer.BaseStream.Position + 3) & ~3) + (writer.BaseStream.Position % 4 == 0 ? 4 : 0),
            SeekOrigin.Begin);

        // Write the number of 4 byte chunks the code takes up, which luckily is just the size of the Code array
        // since it's an array of 4-byte chunks
        writer.Write(cheat.Code.Count);

        // Write the code
        foreach (var chunk in cheat.Code.SelectMany(code => code))
        {
            writer.Write(chunk);
        }

        return Task.CompletedTask;
    }

    private static async Task SerializeFolderAsync(BinaryWriter writer, R4Folder folder)
    {
        // Write the number of items, written to the next two bytes
        writer.Write((ushort)Math.Min(folder.ItemCount, ushort.MaxValue));

        // Write the one-hot flag
        writer.Write(folder.OneHot ? (ushort)0x1100 : (ushort)0x1000);

        // Write the title
        writer.Write(folder.Title.ToCharArray());

        // Write the separator byte
        writer.Write((byte)0x00);

        // Write the description
        writer.Write(folder.Description.ToCharArray());

        // Seek to the next 4-byte border
        writer.Seek((int)((writer.BaseStream.Position + 3) & ~3) + (writer.BaseStream.Position % 4 == 0 ? 4 : 0),
            SeekOrigin.Begin);

        // Write the items
        foreach (var cheat in folder.Cheats)
        {
            await SerializeCheatAsync(writer, cheat);
        }
    }

    private List<(R4Game Game, uint Offset)> CalculateGameAddresses()
    {
        // This is the offset where the game addresses begin. We need to account for this
        uint currentOffset = 0x100;

        var addressBlockSize = (uint)(_r4Database.Games.Count * 16);

        // There is a 16 byte separator that separates the address block from the game section 
        currentOffset += addressBlockSize + 16;

        // Calculate offsets for each game
        var offsets = new List<(R4Game Game, uint Offset)>();

        foreach (var game in _r4Database.Games)
        {
            offsets.Add((game, currentOffset));
            currentOffset += game.Size; // Add the size of the game to the offset
        }

        return offsets;
    }
}