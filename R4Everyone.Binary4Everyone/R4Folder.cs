using System.Text;

namespace R4Everyone.Binary4Everyone;

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