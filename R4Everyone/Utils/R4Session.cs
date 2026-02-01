using R4Everyone.Binary4Everyone;

namespace R4Everyone.Utils;

public sealed class R4Session : IR4Session
{
    public R4Database? Current { get; private set; }
    public string? Path { get; private set; }
    public bool IsDirty { get; private set; }

    public Task NewAsync()
    {
        // Create a new DB in memory (no file path yet)
        Current = new R4Database
        {
            Title = "User cheat code v1.0",
            FileEncoding = R4Encoding.UTF8,
            Enabled = true
        };
        Path = null;
        IsDirty = false;
        return Task.CompletedTask;
    }

    public async Task OpenAsync(string path)
    {
        Current = await R4CheatDat.LoadAsync(path);
        Path = path;
        IsDirty = false;
    }

    public async Task SaveAsync()
    {
        if (Current is null)
            throw new InvalidOperationException("No database is open.");

        if (string.IsNullOrWhiteSpace(Path))
            throw new InvalidOperationException("No path is associated with the current database. Use SaveAs.");

        await R4CheatDat.SaveAsync(Current, Path);
        IsDirty = false;
    }

    public async Task SaveAsAsync(string path)
    {
        if (Current is null)
            throw new InvalidOperationException("No database is open.");

        await R4CheatDat.SaveAsync(Current, path);
        Path = path;
        IsDirty = false;
    }

    public Task CloseAsync()
    {
        Current = null;
        Path = null;
        IsDirty = false;
        return Task.CompletedTask;
    }

    public void MarkDirty() => IsDirty = true;
}