namespace R4Everyone.Binary4Everyone;

/// <summary>
/// An enum representing the possible encoding types for R4 databases
/// </summary>
public enum R4Encoding
{
    [EncodingBytes(0xD5, 0x53, 0x41, 0x59)] // GBK
    GBK,

    [EncodingBytes(0xF5, 0x53, 0x41, 0x59)] // BIG5
    BIG5,

    [EncodingBytes(0x75, 0x53, 0x41, 0x59)] // SJIS
    SJIS,

    [EncodingBytes(0x55, 0x73, 0x41, 0x59)] // UTF8
    UTF8
}

[AttributeUsage(AttributeTargets.Field)]
internal sealed class EncodingBytesAttribute(params byte[] bytes) : Attribute
{
    public byte[] Bytes { get; } = bytes;
}

/// <summary>
/// A helper class that gets the encoding bytes for a given R4Encoding enum value. Each value is annotated with the EncodingBytesAttribute to allow for this
/// </summary>
public static class R4EncodingHelper
{
    public static byte[] GetBytes(R4Encoding encoding)
    {
        var field = encoding.GetType().GetField(encoding.ToString());
        if (field == null) throw new ArgumentException("Invalid encoding", nameof(encoding));

        var attribute = Attribute.GetCustomAttribute(field, typeof(EncodingBytesAttribute));
        if (attribute == null) throw new InvalidOperationException("EncodingBytesAttribute not found");

        return ((EncodingBytesAttribute)attribute).Bytes;
    }

    public static R4Encoding GetEncoding(byte[] bytes)
    {
        foreach (var encoding in Enum.GetValues<R4Encoding>())
        {
            var encodingBytes = GetBytes(encoding);
            if (encodingBytes.Length != bytes.Length) continue;

            var match = !encodingBytes.Where((t, i) => t != bytes[i]).Any();

            if (match) return encoding;
        }

        throw new ArgumentException("Invalid encoding bytes", nameof(bytes));
    }
}