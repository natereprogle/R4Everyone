using System.Text;
using Serilog;

namespace R4Everyone.Binary4Everyone;

public sealed class R4Database : IAsyncDisposable
{
    private const int HeaderSize = 0x100;
    private const string MagicString = "R4 CheatCode";

    internal byte[]? SnapshotBytes { get; private set; }
    internal Dictionary<R4Game, R4LazyGameSlot> LazySlots { get; } = new();

    public R4Encoding FileEncoding;
    public string Title = "User cheat code v1.0";
    public bool Enabled { get; set; }
    public string? R4FilePath;

    public List<R4Game> Games { get; } = [];

    public R4Database()
    {
    }

    public R4Database(string r4FilePath, string title, R4Encoding encoding, bool enabled)
    {
        R4FilePath = r4FilePath;
        Title = title;
        FileEncoding = encoding;
        Enabled = enabled;
    }

    public void EnsureGameMaterialized(R4Game game)
    {
        if (LazySlots.TryGetValue(game, out var slot))
        {
            slot.EnsureMaterialized();
        }
    }

    public void MaterializeAllGames()
    {
        foreach (var slot in LazySlots.Values)
        {
            slot.EnsureMaterialized();
        }
    }

    public void MarkGameDirty(R4Game game)
    {
        if (LazySlots.TryGetValue(game, out var slot))
        {
            slot.MarkDirty();
            return;
        }

        LazySlots[game] = R4LazyGameSlot.CreateNew(this, game);
    }

    internal void SetSnapshot(byte[] snapshotBytes)
    {
        SnapshotBytes = snapshotBytes;
    }

    internal R4LazyGameSlot GetOrCreateSlot(R4Game game)
    {
        if (LazySlots.TryGetValue(game, out var slot))
        {
            return slot;
        }

        var created = R4LazyGameSlot.CreateNew(this, game);
        LazySlots[game] = created;
        return created;
    }

    public static async Task ValidateDatabaseAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await ValidateDatabaseAsync(fs);
    }

    public static Task ValidateDatabaseAsync(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new InvalidOperationException("Stream must be readable and seekable.");

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        stream.Seek(0, SeekOrigin.Begin);

        var header = reader.ReadBytes(HeaderSize);
        // Validate that the database is at minimum 100 bytes. If not, we can guarantee it's invalid
        if (header.Length < HeaderSize)
            throw new FileLoadException("File size is smaller than the minimum required size for an R4 cheat database");
        Log.Verbose("File is at least 100 bytes long, checking magic string");
        Console.WriteLine("File is at least 100 bytes long, checking magic string");

        // Validate that the first 12 bytes are the magic string, which is required for an R4 cheat database
        var magicString = Encoding.ASCII.GetString(header, 0, 12);
        if (magicString != MagicString)
            throw new FileLoadException("Database header could not be verified");
        Log.Verbose("Magic string is correct, checking encoding method");

        // Validate that the bytes at 0x4C and 0x4D are a valid encoding method. Technically we could ignore the last
        // two bytes, and we could even validate just on the first byte, but we're being extra safe here.
        // This is checking that at least one of the encoding methods matches the bytes at these locations and, if so,
        // returns that value so we can set the file encoding
        var isValidEncoding = Enum.GetValues<R4Encoding>().Select(R4EncodingHelper.GetBytes)
            .Any(bytes => bytes[0] == header[0x4C] && bytes[1] == header[0x4D] && bytes[2] == header[0x4E] &&
                          bytes[3] == header[0x4F]);

        // We've already passed the last check, so we just need to return its value since we know everything was true up to this point
        if (!isValidEncoding)
            throw new FileLoadException(
                "Encoding method is not valid");

        Log.Verbose("Encoding method is valid");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles disposing of the database, and flushes the log
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Log.CloseAndFlushAsync();
        await Task.CompletedTask;
    }
}

