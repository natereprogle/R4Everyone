namespace R4Everyone.Binary4Everyone;

/// <summary>
/// An enum representing the possible encoding types for R4 databases
/// </summary>
public enum R4Encoding
{
    [EncodingBytes(0xD5, 0x53, 0x41, 0x59)] // GBK
    // ReSharper disable once InconsistentNaming
    GBK,

    [EncodingBytes(0xF5, 0x53, 0x41, 0x59)] // BIG5
    // ReSharper disable once InconsistentNaming
    BIG5,

    [EncodingBytes(0x75, 0x53, 0x41, 0x59)] // SJIS
    // ReSharper disable once InconsistentNaming
    SJIS,

    [EncodingBytes(0x55, 0x73, 0x41, 0x59)] // UTF8
    // ReSharper disable once InconsistentNaming
    UTF8,

    // Some real-world R4 databases leave the encoding marker zeroed instead of
    // writing one of the signatures above. We accept the all-zero marker rather
    // than rejecting an otherwise-valid file, and write it back unchanged on
    // save. Text is currently decoded as UTF-8 (see R4Binary.CurrentEncoding).
    [EncodingBytes(0x00, 0x00, 0x00, 0x00)]
    Default
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
        return attribute == null ? throw new InvalidOperationException("EncodingBytesAttribute not found") : ((EncodingBytesAttribute)attribute).Bytes;
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