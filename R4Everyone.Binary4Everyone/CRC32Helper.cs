using System.IO.Hashing;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// A small helper utility to calculate the CRC32 checksum that R4 specifically looks for. It uses CRC32, but it's inverted
/// </summary>
public static class Crc32Helper
{
    /// <summary>
    /// Calculate the CRC32 checksum for the given data. This method is meant for NDS ROMs for use with R4 cheat databases
    /// Pass in the first 512 bytes of the NDS ROM, which is the header, to get the correct checksum
    /// </summary>
    /// <param name="data">The data to calculate the checksum for, usually the first 512 bytes of an NDS ROM</param>
    /// <returns>The NDS ROM checksum</returns>
    public static uint CalculateCrc32(byte[] data)
    {
        // Compute the CRC32 checksum for the input data
        var checksum = Crc32.Hash(data);

        // Return the checksum as a hexadecimal string, inverted (R4 format)
        var invertedChecksum = BitConverter.ToUInt32(checksum, 0) ^ 0xffffffff;
        Console.WriteLine("Checksum is " + invertedChecksum.ToString("x8"));
        return invertedChecksum;
    }

    public static string ConvertCrc32ToString(uint crc32)
    {
        return crc32.ToString("x8");
    }
}