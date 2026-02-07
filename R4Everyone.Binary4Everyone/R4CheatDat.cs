namespace R4Everyone.Binary4Everyone;

public static class R4CheatDat
{
    private static readonly R4DatabaseCodec Codec = new();

    public static Task<R4Database> LoadAsync(string filePath) => R4DatabaseCodec.LoadAsync(filePath);

    public static Task<R4Database> LoadAsync(Stream stream) => R4DatabaseCodec.LoadAsync(stream);

    public static Task SaveAsync(R4Database db, string? filePathOverride = null) => Codec.SaveAsync(db, filePathOverride);

    public static Task SaveAsync(R4Database db, Stream stream) => Codec.SaveAsync(db, stream);
}
