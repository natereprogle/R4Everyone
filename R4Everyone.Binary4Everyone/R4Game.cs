using System.Text;
using Serilog;

namespace R4Everyone.Binary4Everyone;

// public class R4Game(string id)
// {
//     /// <summary>
//     /// The game's ID
//     /// </summary>
//     public string GameId { get; set; } = id;
//
//     /// <summary>
//     /// The title of the game
//     /// </summary>
//     public string GameTitle { get; set; } = "My Game Title";
//
//     /// <summary>
//     /// Whether the game's cheats are enabled
//     /// </summary>
//     public bool GameEnabled { get; set; }
//
//     /// <summary>
//     /// The game's checksum. Calculated via the CRC32 Helper
//     /// </summary>
//     public string GameChecksum { get; set; } = "2F00B549";
//
//     public uint[] MasterCodes { get; set; } =
//         [0x00000000, 0x01000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x00000000];
//
//     /// Nested cheats and folders within this folder. Uses a List&lt;object&gt; to maintain order
//     public List<object> Items { get; } = [];
//
//     /// <summary>
//     /// The game's size, calculated by adding the sizes of all folders and cheats
//     /// This does not take into account the ID or Checksum, as those are used in the address block of the R4 database and not the game itself
//     /// </summary>
//     public uint Size
//     {
//         get
//         {
//             // Step 1: Calculate the size of the title
//             var titleLength = Encoding.UTF8.GetByteCount(GameTitle);
//             var paddingNeeded = (4 - titleLength % 4) % 4;
//
//             // If length is a multiple of 4, add an additional 4 bytes of padding
//             if (paddingNeeded == 0)
//             {
//                 paddingNeeded = 4;
//             }
//
//             var totalSize = (uint)(titleLength + paddingNeeded);
//
//             // Account for the 4 bytes for the item count and enable flag, and the 32 byte master code
//             totalSize += 4 + 32;
//
//             // Add sizes of all folders and cheats and return
//             totalSize += (uint)Items.Sum(item => item switch
//             {
//                 R4Folder folder => folder.Size,
//                 R4Cheat cheat => cheat.Size,
//                 _ => 0
//             });
//
//             return totalSize;
//         }
//     }
//
//     public uint ItemCount
//     {
//         get
//         {
//             // Add sizes of all folders and cheats and return
//             return (uint)Items.Sum(item => item switch
//             {
//                 R4Folder folder => 1 + folder.ItemCount,
//                 R4Cheat => 1,
//                 _ => 0
//             });
//         }
//     }
// }
//
// public class R4GameDeserializer(R4Game game)
// {
//     private R4Game Game { get; } = game;
//
//     public async Task<R4Game> Deserialize(BinaryReader reader)
//     {
//         ArgumentNullException.ThrowIfNull(reader);
//         if (reader.BaseStream is not { CanRead: true })
//             throw new IOException(
//                 "Could not read from the file stream, did the file get locked or deleted by another process?");
//
//         Game.GameTitle = ReadItemMeta(reader, true).Title;
//         var numItems = reader.ReadUInt16();
//
//         var masterEnabled = reader.ReadBytes(2)[1] == 240;
//         Game.GameEnabled = masterEnabled;
//
//         for (var masterCodeSegment = 0; masterCodeSegment < 8; masterCodeSegment++)
//         {
//             Game.MasterCodes[masterCodeSegment] = BitConverter.ToUInt32(reader.ReadBytes(4));
//         }
//
//         var itemsRead = 0;
//
//         // The number of items read in this call can vary depending on type of item. 
//         // For example, a cheat will always be one item, but a folder can be one or more items, in the case of nested folders and/or cheats
//         // Instead of doing a for loop over the number of items, we just do a while loop and start reading, since folders are read recursively
//         while (itemsRead < numItems)
//         {
//             itemsRead += await ParseItemsAsync(reader, Game);
//         }
//
//         return Game;
//     }
//
//     private static async Task<int> ParseItemsAsync(BinaryReader reader, dynamic currentContainer)
//     {
//         // We need to keep track of the number of items read inside this call to return, since we can read recursively
//         var itemsRead = 0;
//
//         ArgumentNullException.ThrowIfNull(reader);
//         if (currentContainer == null) throw new ArgumentNullException(nameof(currentContainer));
//
//         try
//         {
//             var numItems = reader.ReadUInt16();
//             var itemType = reader.ReadUInt16();
//             var title = ReadItemMeta(reader);
//             uint numChunks = 0;
//
//             if (itemType is not (0x1000 or 0x1100))
//             {
//                 var chunks = reader.ReadBytes(4);
//                 if (chunks.Length < 4) throw new InvalidDataException("Incomplete data for item chunk count.");
//                 numChunks = (uint)(chunks[0] | (chunks[1] << 8) | (chunks[2] << 16) | (chunks[3] << 24));
//             }
//
//             switch (itemType)
//             {
//                 // Enabled/disabled folder
//                 case 0x1000:
//                 case 0x1100:
//                     var folder = new R4Folder
//                     {
//                         Title = title.Title,
//                         Description = title.Description ?? "",
//                         OneHot = itemType == 0x1100
//                     };
//
//                     // We want to add 1 for the folder itself, as well as any items read within the folder
//                     itemsRead++;
//
//                     for (var i = 0; i < numItems; i++)
//                     {
//                         itemsRead += await ParseItemsAsync(reader, folder);
//                     }
//
//                     // Add the folder to the game
//                     currentContainer.Items.Add(folder);
//
//                     break;
//
//                 // Enabled/disabled cheat
//                 case 0x0100:
//                 case 0x0000:
//                     var cheat = new R4Cheat
//                     {
//                         Title = title.Title,
//                         Description = title.Description ?? "",
//                         Enabled = itemType == 0x0100,
//                         Code = ReadCheatCodes(reader, numChunks)
//                     };
//
//                     // Adds to the current container's cheats list to allow folders to contain cheats
//                     switch (currentContainer)
//                     {
//                         case R4Game:
//                             currentContainer.Items.Add(cheat);
//                             break;
//                         case R4Folder:
//                             currentContainer.Cheats.Add(cheat);
//                             break;
//                     }
//
//                     itemsRead++;
//                     break;
//
//                 default:
//                     throw new NotSupportedException($"Unknown item type encountered: 0x{itemType:X4}");
//             }
//         }
//         catch (EndOfStreamException ex)
//         {
//             throw new InvalidDataException("Unexpected end of stream while parsing items.", ex);
//         }
//
//         // Return the final number of items read
//         return itemsRead;
//     }
//
//
//     // Helper function to read the title (null-terminated, padded to 4 bytes)
//     private static (string Title, string? Description) ReadItemMeta(BinaryReader reader, bool game = false)
//     {
//         ArgumentNullException.ThrowIfNull(reader);
//         var titleBytes = new List<byte>();
//
//         try
//         {
//             Log.Verbose("Reading item metadata, beginning with title");
//             byte bx;
//             while ((bx = reader.ReadByte()) != 0)
//             {
//                 titleBytes.Add(bx);
//             }
//
//             if (game)
//             {
//                 reader.BaseStream.Seek((4 - reader.BaseStream.Position % 4) % 4, SeekOrigin.Current);
//                 var title = Encoding.ASCII.GetString(titleBytes.ToArray());
//                 Log.Debug("Parsing game: {Title}", title);
//                 Log.Verbose(
//                     "Skipping description for game metadata, item is a game which don't have descriptions. Pointer is at {Position}",
//                     reader.BaseStream.Position);
//                 return (title, null);
//             }
//
//             Log.Verbose("Item is either a folder or a cheat, reading description. Pointer is at {Position}",
//                 reader.BaseStream.Position);
//             var descriptionBytes = new List<byte>();
//             byte by;
//             while ((by = reader.ReadByte()) != 0)
//             {
//                 descriptionBytes.Add(by);
//             }
//
//             Log.Verbose(
//                 "Completed description read, moving to the end of the nearest 4-byte boundary. Pointer is at {Position}",
//                 reader.BaseStream.Position);
//             reader.BaseStream.Seek((4 - reader.BaseStream.Position % 4) % 4, SeekOrigin.Current);
//             Log.Verbose("Moved pointer, now at {Position}",
//                 reader.BaseStream.Position);
//             Log.Debug("Parsing item {Title}, Description: {Description}",
//                 Encoding.ASCII.GetString(titleBytes.ToArray()), Encoding.ASCII.GetString(descriptionBytes.ToArray()));
//
//             return (Encoding.ASCII.GetString(titleBytes.ToArray()),
//                 Encoding.ASCII.GetString(descriptionBytes.ToArray()));
//         }
//         catch (EndOfStreamException ex)
//         {
//             throw new InvalidDataException("Unexpected end of stream while reading metadata.", ex);
//         }
//         catch (Exception ex)
//         {
//             throw new InvalidDataException("Failed to read item metadata.", ex);
//         }
//     }
//
//
//     // Helper function to read cheat codes (4-byte chunks)
//     private static List<byte[]> ReadCheatCodes(BinaryReader reader, ulong numChunks)
//     {
//         ArgumentNullException.ThrowIfNull(reader);
//         if (numChunks == 0) return [];
//
//         var cheatCodes = new List<byte[]>();
//
//         try
//         {
//             Log.Verbose("Starting to read {NumChunks} cheat code chunks.", numChunks);
//             for (ulong i = 0; i < numChunks; i++)
//             {
//                 var cheatCode = reader.ReadBytes(4);
//                 if (cheatCode.Length < 4)
//                     throw new InvalidDataException("Incomplete cheat code found.");
//                 cheatCodes.Add(cheatCode);
//                 Log.Verbose("Read cheat code chunk {Index}: {CheatCode}", i, BitConverter.ToString(cheatCode));
//             }
//
//             Log.Debug("Read cheat codes: {CheatCodes}", cheatCodes.Select(c => BitConverter.ToString(c)));
//             return cheatCodes;
//         }
//         catch (EndOfStreamException ex)
//         {
//             Log.Error(ex, "Unexpected end of stream while reading cheat codes.");
//             throw new InvalidDataException("Unexpected end of stream while reading cheat codes.", ex);
//         }
//     }
// }

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