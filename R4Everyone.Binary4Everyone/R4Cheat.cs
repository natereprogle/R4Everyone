using System.Text;

namespace R4Everyone.Binary4Everyone;

public sealed class R4Cheat : IR4Item
{
    public string Title { get; set; } = "New Cheat";
    public string Description { get; set; } = "New Cheat Description";
    public bool Enabled { get; set; } = true;
    
    public List<byte[]> Code { get; set; } = [];

    public uint Size
    {
        get
        {
            var combinedLength = Encoding.UTF8.GetByteCount(Title) + 1 + Encoding.UTF8.GetByteCount(Description);
            var paddingNeeded = (4 - combinedLength % 4) % 4;
            if (paddingNeeded == 0) paddingNeeded = 4;
            return (uint)(combinedLength + paddingNeeded) + 8 + (uint)Code.Count * 4;
        }
    }
}