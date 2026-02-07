using System.Text;

namespace R4Everyone.Binary4Everyone;

public sealed class R4Game : IR4Container
{
    public R4Game(string id) => GameId = id;

    /// <summary>
    /// The game's ID
    /// </summary>
    public string GameId { get; set; }
    
    /// <summary>
    /// The title of the game
    /// </summary>
    public string GameTitle { get; set; } = "My Game Title";

    /// <summary>
    /// Whether the game's cheats are enabled
    /// </summary>
    public bool GameEnabled { get; set; }

    /// <summary>
    /// The game's checksum. Calculated via the CRC32 Helper
    /// </summary>
    public string GameChecksum { get; set; } = "2F00B549";

    public uint[] MasterCodes { get; set; } =
        [0x00000000, 0x01000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000];

    /// Nested cheats and folders within this folder. Uses a List&lt;object&gt; to maintain order
    public List<IR4Item> Items { get; } = [];

    /// <summary>
    /// The game's size, calculated by adding the sizes of all folders and cheats
    /// This does not take into account the ID or Checksum, as those are used in the address block of the R4 database and not the game itself
    /// </summary>
    public uint Size
    {
        get
        {
            var titleLength = Encoding.UTF8.GetByteCount(GameTitle);
            var paddingNeeded = (4 - titleLength % 4) % 4;
            if (paddingNeeded == 0) paddingNeeded = 4;

            var totalSize = (uint)(titleLength + paddingNeeded);
            totalSize += 4 + 32; // item count + enabled flag + master codes

            return Items.Aggregate(totalSize, (current, item) => current + item switch
            {
                R4Folder f => f.Size,
                R4Cheat c => c.Size,
                _ => 0
            });
        }
    }
    
    public uint FlattenedItemCount => (uint)CountFlattened(Items);

    private static int CountFlattened(IEnumerable<IR4Item> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            count += 1;
            if (item is R4Folder f)
                count += CountFlattened(f.Items);
        }

        return count;
    }
}