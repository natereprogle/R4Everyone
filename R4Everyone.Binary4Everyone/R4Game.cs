using System.Text;

namespace R4Everyone.Binary4Everyone;

public class R4Game(string id)
{
    // These values are required during initialization, because we get them from the pointer section, which is before the game section
    public string GameId { get; set; } = id;

    // These can be set at any time
    public string GameTitle = "My Game Title";
    public bool GameEnabled { get; set; }
    public uint GameChecksum { get; set; }

    public uint[] MasterCodes { get; set; } =
        [0x00000000, 0x01000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000];

    public List<R4Folder> Folders { get; } = [];
    public List<R4Cheat> Cheats { get; } = [];
}

public class R4GameDeserializer(R4Game game)
{
    private R4Game Game { get; } = game;

    public async Task<R4Game> Deserialize(BinaryReader reader)
    {
        Game.GameTitle = ReadItemMeta(reader, true).Item1;
        var numItems = reader.ReadUInt16();

        var masterEnabled = reader.ReadBytes(2)[1] == 240;

        for (var masterCodeSegment = 0; masterCodeSegment < 8; masterCodeSegment++)
        {
            Game.MasterCodes[masterCodeSegment] = BitConverter.ToUInt32(reader.ReadBytes(4));
        }

        for (var i = 0; i < numItems; i++)
        {
            await ParseItemsAsync(reader, Game);
        }

        return Game;
    }

    private static async Task ParseItemsAsync(BinaryReader reader, dynamic currentContainer)
    {
        var numItems = reader.ReadUInt16(); // Number of items in the current game or folder

        for (var i = 0; i < numItems; i++)
        {
            var itemType = reader.ReadUInt16();
            var title = ReadItemMeta(reader);
            uint numChunks = 0;
            var enabled = false;
            if (itemType is not (0x1000 or 0x1100))
            {
                var chunks = reader.ReadBytes(3);
                numChunks = (uint)(chunks[0] | (chunks[1] << 8) | (chunks[2] << 16));
                enabled = reader.ReadBytes(2)[1] == 1;
            }

            switch (itemType)
            {
                // Folder
                case 0x1000:
                case 0x1100:
                {
                    var folder = new R4Folder
                    {
                        Title = title.Item1,
                        Description = title.Item2 ?? "",
                        OneHot = itemType == 0x1100
                    };

                    // Recursively parse items within the folder
                    await ParseItemsAsync(reader, folder);

                    // Add folder to the current container (R4Game or R4Folder)
                    currentContainer.Folders.Add(folder);
                    break;
                }
                // Cheat
                case 0x0100:
                case 0x0000:
                {
                    var cheat = new R4Cheat
                    {
                        Title = title.Item1,
                        Description = title.Item2 ?? "",
                        Enabled = enabled,
                        Code = ReadCheatCodes(reader, numChunks)
                    };

                    // Add cheat to the current container
                    currentContainer.Cheats.Add(cheat);
                    break;
                }
            }
        }
    }

    // Helper function to read the title (null-terminated, padded to 4 bytes)
    private static (string, string?) ReadItemMeta(BinaryReader reader, bool game = false)
    {
        var titleBytes = new List<byte>();
        byte bx;
        while ((bx = reader.ReadByte()) != 0)
        {
            titleBytes.Add(bx);
        }

        // Early return if it's a game, since games don't have descriptions
        if (game)
        {
            reader.BaseStream.Seek((4 - titleBytes.Count % 4) % 4 - 1, SeekOrigin.Current);
            Console.WriteLine("Parsing game: " + Encoding.ASCII.GetString(titleBytes.ToArray()));
            return (Encoding.ASCII.GetString(titleBytes.ToArray()), null);
        }

        var descriptionBytes = new List<byte>();
        byte by;
        while ((by = reader.ReadByte()) != 0)
        {
            descriptionBytes.Add(by);
        }

        reader.BaseStream.Seek((4 - (titleBytes.Count + 1 + descriptionBytes.Count) % 4) % 4 - 1, SeekOrigin.Current);

        if ((titleBytes.Count + 1 + descriptionBytes.Count) % 4 == 0)
        {
            reader.BaseStream.Seek(4, SeekOrigin.Current);
        }

        Console.WriteLine("Parsing item: " + Encoding.ASCII.GetString(titleBytes.ToArray()));
        return (Encoding.ASCII.GetString(titleBytes.ToArray()), Encoding.ASCII.GetString(descriptionBytes.ToArray()));
    }

    // Helper function to read cheat codes (4-byte chunks)
    private static List<byte[]> ReadCheatCodes(BinaryReader reader, ulong numChunks)
    {
        var cheatCodes = new List<byte[]>();

        for (ulong i = 0; i < numChunks; i++)
        {
            var cheatCode = reader.ReadBytes(4); // 4-byte chunks
            cheatCodes.Add(cheatCode);
        }

        return cheatCodes;
    }
}