using System.Text;

namespace R4Everyone.Binary4Everyone;

// public class R4Folder
// {
//     /// <summary>
//     /// The title of the folder
//     /// </summary>
//     public string Title { get; set; } = "New Folder";
//
//     /// <summary>
//     /// The description of the folder
//     /// </summary>
//     public string Description { get; set; } = "New Folder Description";
//
//     /// <summary>
//     /// Determines whether more than one cheat can be active, or "hot", in the folder at a time
//     /// </summary>
//     public bool OneHot { get; set; } = false;
//
//     /// <summary>
//     /// Nested cheats within this folder
//     /// </summary>
//     public List<R4Cheat> Cheats { get; } = [];
//
//     /// <summary>
//     /// Calculate the size of the folder
//     /// </summary>
//     public uint Size
//     {
//         get
//         {
//             // Combine title and description with a 0x00 separator
//             var combinedLength = System.Text.Encoding.UTF8.GetByteCount(Title) + 1 +
//                                  System.Text.Encoding.UTF8.GetByteCount(Description);
//
//             // Calculate padding to meet the 4*n-1 requirement
//             var paddingNeeded = (4 - combinedLength % 4) % 4;
//
//             // If length is a multiple of 4, add an additional 4 bytes of padding
//             if (paddingNeeded == 0)
//             {
//                 paddingNeeded = 4;
//             }
//
//             // Keep track of the size of the folder thus far
//             var totalSize = (uint)(combinedLength + paddingNeeded + 4);
//
//             // Calculate the size of the nested cheats
//             Cheats.ForEach(cheat => totalSize += cheat.Size);
//
//             // Add 4 to account for the number of 4 byte chunks and one-hot flag, and add the length of the code itself
//             return totalSize;
//         }
//     }
//
//     public uint ItemCount => (uint)Cheats.Count;
// }

public sealed class R4Folder : IR4Item, IR4Container
{
    /// <summary>
    /// The title of the folder
    /// </summary>
    public string Title { get; set; } = "New Folder";

    /// <summary>
    /// The description of the folder
    /// </summary>
    public string Description { get; set; } = "New Folder Description";

    /// <summary>
    /// Determines whether more than one cheat can be active, or "hot", in the folder at a time
    /// </summary>
    public bool OneHot { get; set; }

    public List<IR4Item> Items { get; } = [];

    public uint Size
    {
        get
        {
            var combinedLength = Encoding.UTF8.GetByteCount(Title) + 1 + Encoding.UTF8.GetByteCount(Description);
            var paddingNeeded = (4 - combinedLength % 4) % 4;
            if (paddingNeeded == 0) paddingNeeded = 4;

            var totalSize = (uint)(combinedLength + paddingNeeded + 4); // 2 ushorts

            return Items.Aggregate(totalSize, (current, item) => current + item switch
            {
                R4Cheat c => c.Size,
                R4Folder f => f.Size,
                _ => 0
            });
        }
    }
}