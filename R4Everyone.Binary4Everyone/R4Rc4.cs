namespace R4Everyone.Binary4Everyone;

/// <summary>
/// Textbook RC4. Encryption and decryption are the same operation, and every call starts a fresh keystream, which is
/// how r4cce encrypts each region of a CycloDS Evolution or EDGE file.
/// </summary>
internal static class R4Rc4
{
    public static byte[] Apply(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("The RC4 key must not be empty.", nameof(key));
        }

        Span<byte> s = stackalloc byte[256];
        for (var i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var output = data.ToArray();
        var x = 0;
        j = 0;
        for (var n = 0; n < output.Length; n++)
        {
            x = (x + 1) & 0xFF;
            j = (j + s[x]) & 0xFF;
            (s[x], s[j]) = (s[j], s[x]);
            output[n] ^= s[(s[x] + s[j]) & 0xFF];
        }

        return output;
    }
}
