using System.Buffers.Binary;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// A managed MD5 (RFC 1321) implementation. <c>System.Security.Cryptography.MD5</c> throws
/// <see cref="PlatformNotSupportedException"/> in Blazor WebAssembly, so the CycloDS Evolution and EDGE codecs use this
/// instead. MD5 is only used here for file integrity checks, never for security.
/// </summary>
public static class R4Md5
{
    public const int HashSizeInBytes = 16;

    private static readonly int[] Shifts =
    [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21
    ];

    // K[i] = floor(abs(sin(i + 1)) * 2^32)
    private static readonly uint[] K =
    [
        0xD76AA478, 0xE8C7B756, 0x242070DB, 0xC1BDCEEE, 0xF57C0FAF, 0x4787C62A, 0xA8304613, 0xFD469501,
        0x698098D8, 0x8B44F7AF, 0xFFFF5BB1, 0x895CD7BE, 0x6B901122, 0xFD987193, 0xA679438E, 0x49B40821,
        0xF61E2562, 0xC040B340, 0x265E5A51, 0xE9B6C7AA, 0xD62F105D, 0x02441453, 0xD8A1E681, 0xE7D3FBC8,
        0x21E1CDE6, 0xC33707D6, 0xF4D50D87, 0x455A14ED, 0xA9E3E905, 0xFCEFA3F8, 0x676F02D9, 0x8D2A4C8A,
        0xFFFA3942, 0x8771F681, 0x6D9D6122, 0xFDE5380C, 0xA4BEEA44, 0x4BDECFA9, 0xF6BB4B60, 0xBEBFBC70,
        0x289B7EC6, 0xEAA127FA, 0xD4EF3085, 0x04881D05, 0xD9D4D039, 0xE6DB99E5, 0x1FA27CF8, 0xC4AC5665,
        0xF4292244, 0x432AFF97, 0xAB9423A7, 0xFC93A039, 0x655B59C3, 0x8F0CCC92, 0xFFEFF47D, 0x85845DD1,
        0x6FA87E4F, 0xFE2CE6E0, 0xA3014314, 0x4E0811A1, 0xF7537E82, 0xBD3AF235, 0x2AD7D2BB, 0xEB86D391
    ];

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        uint a0 = 0x67452301, b0 = 0xEFCDAB89, c0 = 0x98BADCFE, d0 = 0x10325476;

        var fullBlocks = data.Length / 64;
        for (var i = 0; i < fullBlocks; i++)
        {
            ProcessBlock(data.Slice(i * 64, 64), ref a0, ref b0, ref c0, ref d0);
        }

        // Padding: 0x80, zeros, then the message length in bits as a little-endian u64.
        var remaining = data[(fullBlocks * 64)..];
        Span<byte> tail = stackalloc byte[128];
        tail.Clear();
        remaining.CopyTo(tail);
        tail[remaining.Length] = 0x80;
        var tailLength = remaining.Length + 1 + 8 <= 64 ? 64 : 128;
        BinaryPrimitives.WriteUInt64LittleEndian(tail[(tailLength - 8)..], (ulong)data.Length * 8);
        for (var offset = 0; offset < tailLength; offset += 64)
        {
            ProcessBlock(tail.Slice(offset, 64), ref a0, ref b0, ref c0, ref d0);
        }

        var hash = new byte[HashSizeInBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(hash.AsSpan(0), a0);
        BinaryPrimitives.WriteUInt32LittleEndian(hash.AsSpan(4), b0);
        BinaryPrimitives.WriteUInt32LittleEndian(hash.AsSpan(8), c0);
        BinaryPrimitives.WriteUInt32LittleEndian(hash.AsSpan(12), d0);
        return hash;
    }

    private static void ProcessBlock(ReadOnlySpan<byte> block, ref uint a0, ref uint b0, ref uint c0, ref uint d0)
    {
        Span<uint> m = stackalloc uint[16];
        for (var i = 0; i < 16; i++)
        {
            m[i] = BinaryPrimitives.ReadUInt32LittleEndian(block[(i * 4)..]);
        }

        uint a = a0, b = b0, c = c0, d = d0;
        for (var i = 0; i < 64; i++)
        {
            uint f;
            int g;
            switch (i / 16)
            {
                case 0:
                    f = (b & c) | (~b & d);
                    g = i;
                    break;
                case 1:
                    f = (d & b) | (~d & c);
                    g = (5 * i + 1) % 16;
                    break;
                case 2:
                    f = b ^ c ^ d;
                    g = (3 * i + 5) % 16;
                    break;
                default:
                    f = c ^ (b | ~d);
                    g = 7 * i % 16;
                    break;
            }

            f = f + a + K[i] + m[g];
            a = d;
            d = c;
            c = b;
            b += uint.RotateLeft(f, Shifts[i]);
        }

        a0 += a;
        b0 += b;
        c0 += c;
        d0 += d;
    }
}
