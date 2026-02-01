namespace R4Everyone.Binary4Everyone;

public static class R4CheatDat
{
    private static readonly R4DatabaseCodec Codec = new();

    public static Task<R4Database> LoadAsync(string filePath) => R4DatabaseCodec.LoadAsync(filePath);

    public static Task SaveAsync(R4Database db, string? filePathOverride = null) => Codec.SaveAsync(db, filePathOverride);
}