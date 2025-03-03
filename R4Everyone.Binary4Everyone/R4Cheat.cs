namespace R4Everyone.Binary4Everyone;

public class R4Cheat
{
    public string Title { get; set; } = "New Cheat";
    public string Description { get; set; } = "New Cheat Description";
    public bool Enabled { get; set; } = true;
    public List<byte[]> Code { get; set; } = [];

    public uint Size
    {
        get
        {
            // Combine title and description with a 0x00 separator
            var combinedLength = System.Text.Encoding.UTF8.GetByteCount(Title) + 1 + System.Text.Encoding.UTF8.GetByteCount(Description);

            // Calculate padding to meet the 4*n-1 requirement
            var paddingNeeded = (4 - combinedLength % 4) % 4;

            // If length is a multiple of 4, add an additional 4 bytes of padding
            if (paddingNeeded == 0)
            {
                paddingNeeded = 4;
            }

            // Add 8 to account for the number of 4 byte chunks in the cheat, the enable flag, and the number of 4-byte chunks in the cheat code,
            // and add the length of the code itself
            return (uint)(combinedLength + paddingNeeded) + 8 + (uint)Code.Count * 4;
        }
    }
}