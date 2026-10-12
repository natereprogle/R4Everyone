using System.Text;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

/// <summary>
/// Reads and patches the encoding marker in the header of an R4 cheat database file.
/// </summary>
public static class DatabaseFileHeader
{
    private const int HeaderLength = 0x100;
    private const string MagicString = "R4 CheatCode";
    private const int EncodingMarkerOffset = 0x4C;
    private const int EncodingMarkerLength = 4;

    /// <summary>
    /// Returns true if the file looks like an R4 database but its encoding marker is not one of the known
    /// signatures, for example the all-zero marker some online databases use. Such files need the user to choose an
    /// encoding before they are opened.
    /// </summary>
    public static bool HasUnknownEncodingMarker(byte[] file, out byte[] marker)
    {
        marker = [];
        if (file.Length < HeaderLength ||
            Encoding.ASCII.GetString(file, 0, MagicString.Length) != MagicString)
        {
            // Not an R4 database at all; let the normal load report the problem.
            return false;
        }

        marker = file[EncodingMarkerOffset..(EncodingMarkerOffset + EncodingMarkerLength)];
        var fileMarker = marker;
        return !Enum.GetValues<R4Encoding>().Any(encoding => R4EncodingHelper.GetBytes(encoding).SequenceEqual(fileMarker));
    }

    /// <summary>
    /// Returns a copy of the file with its encoding marker replaced.
    /// </summary>
    public static byte[] WithEncoding(byte[] file, R4Encoding encoding)
    {
        var patched = file.ToArray();
        R4EncodingHelper.GetBytes(encoding).CopyTo(patched, EncodingMarkerOffset);
        return patched;
    }

    public static string FormatMarker(byte[] marker) => string.Join(' ', marker.Select(b => b.ToString("X2")));
}
