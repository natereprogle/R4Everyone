namespace R4Everyone.Binary4Everyone;

/// <summary>
/// The cheat database formats this library can read or write.
/// </summary>
public enum R4CheatFormat
{
    /// <summary>R4 <c>usrcheat.dat</c>. Read and write.</summary>
    R4,

    /// <summary>CycloDS Evolution <c>.evoCheats</c>. Read and write.</summary>
    CycloDsEvolution,

    /// <summary>EDGE <c>.dat</c>. Read and write.</summary>
    Edge,

    /// <summary>M3 DS Real <c>.db</c>. Read and write.</summary>
    M3Real
}

/// <summary>
/// Detects, loads, and saves any supported cheat database format.
/// </summary>
public static class R4CheatFormats
{
    private static readonly byte[] R4Magic = "R4 CheatCode"u8.ToArray();

    /// <summary>
    /// The creator tag R4Everyone writes into CycloDS Evolution and EDGE headers (offset <c>0x24</c>), where r4cce
    /// writes <c>USAY</c>. R4 and M3 DS Real files have no such field and are written unchanged.
    /// </summary>
    public static ReadOnlySpan<byte> CreatorTag => "R4EO"u8;

    /// <summary>
    /// Returns true if files in this format have a creator tag field, so <see cref="IsCreatedByR4Everyone"/> can tell
    /// whether R4Everyone saved them.
    /// </summary>
    public static bool HasCreatorTag(R4CheatFormat format) =>
        format is R4CheatFormat.CycloDsEvolution or R4CheatFormat.Edge;

    /// <summary>
    /// Returns true if the file's creator tag is <see cref="CreatorTag"/>. Always false for formats without a creator
    /// tag (see <see cref="HasCreatorTag"/>).
    /// </summary>
    public static bool IsCreatedByR4Everyone(ReadOnlySpan<byte> data) =>
        Detect(data) is R4CheatFormat.CycloDsEvolution or R4CheatFormat.Edge &&
        R4EvolutionCheats.IsCreatedByR4Everyone(data);

    /// <summary>
    /// Detects a readable format from the file's magic bytes, or for M3 DS Real (which has none) from its index
    /// structure. R4 and EDGE both use <c>.dat</c>, so the extension is never used. Returns null for unrecognized data.
    /// </summary>
    public static R4CheatFormat? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(R4Magic))
        {
            return R4CheatFormat.R4;
        }

        if (R4EvolutionCheats.TryDetect(data, out var variant))
        {
            return ToFormat(variant);
        }

        // M3 DS Real has no magic bytes, so check its index structure last.
        if (R4M3RealDb.TryDetect(data))
        {
            return R4CheatFormat.M3Real;
        }

        return null;
    }

    public static bool CanRead(R4CheatFormat format) => Enum.IsDefined(format);

    public static string GetDisplayName(R4CheatFormat format) => format switch
    {
        R4CheatFormat.R4 => "R4 (usrcheat.dat)",
        R4CheatFormat.CycloDsEvolution => "CycloDS Evolution",
        R4CheatFormat.Edge => "EDGE",
        R4CheatFormat.M3Real => "M3 DS Real",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    /// <summary>The usual file extension, including the leading dot.</summary>
    public static string GetFileExtension(R4CheatFormat format) => format switch
    {
        R4CheatFormat.R4 or R4CheatFormat.Edge => ".dat",
        R4CheatFormat.CycloDsEvolution => ".evoCheats",
        R4CheatFormat.M3Real => ".db",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    /// <summary>
    /// Loads a database in any readable format.
    /// </summary>
    /// <param name="m3RealOptions">Settings for <see cref="R4CheatFormat.M3Real"/> files; null values are detected.</param>
    /// <returns>The database and the format it was read from.</returns>
    /// <exception cref="InvalidDataException">The format is not recognized, or the file is corrupt.</exception>
    public static async Task<(R4Database Database, R4CheatFormat Format)> LoadAsync(byte[] data,
        R4M3RealReadOptions? m3RealOptions = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        switch (Detect(data))
        {
            case R4CheatFormat.R4:
                using (var stream = new MemoryStream(data, writable: false))
                {
                    return (await R4CheatDat.LoadAsync(stream), R4CheatFormat.R4);
                }
            case R4CheatFormat.M3Real:
                return (R4M3RealDb.Load(data, m3RealOptions), R4CheatFormat.M3Real);
            case { } format:
                return (R4EvolutionCheats.Load(data), format);
            default:
                throw new InvalidDataException(
                    "Unrecognized file. Supported formats are R4 usrcheat.dat, CycloDS Evolution .evoCheats, EDGE .dat, and M3 DS Real .db.");
        }
    }

    /// <summary>
    /// Serializes a database in the given format.
    /// </summary>
    /// <param name="m3RealOptions">Options for <see cref="R4CheatFormat.M3Real"/>; ignored for other formats.</param>
    public static async Task<byte[]> SaveAsync(R4Database db, R4CheatFormat format, R4M3RealOptions? m3RealOptions = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        switch (format)
        {
            case R4CheatFormat.R4:
                await using (var stream = new MemoryStream())
                {
                    await R4CheatDat.SaveAsync(db, stream);
                    return stream.ToArray();
                }
            case R4CheatFormat.CycloDsEvolution:
                return R4EvolutionCheats.Save(db, R4EvolutionVariant.CycloDsEvolution);
            case R4CheatFormat.Edge:
                return R4EvolutionCheats.Save(db, R4EvolutionVariant.Edge);
            case R4CheatFormat.M3Real:
                return R4M3RealDb.Export(db, m3RealOptions);
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static R4CheatFormat ToFormat(R4EvolutionVariant variant) => variant == R4EvolutionVariant.Edge
        ? R4CheatFormat.Edge
        : R4CheatFormat.CycloDsEvolution;
}
