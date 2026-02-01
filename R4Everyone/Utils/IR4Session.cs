using R4Everyone.Binary4Everyone;

namespace R4Everyone.Utils;

public interface IR4Session
{
    R4Database? Current { get; }
    string? Path { get; }
    bool IsDirty { get; }

    Task NewAsync();
    Task OpenAsync(string path);
    Task SaveAsync();
    Task SaveAsAsync(string path);
    Task CloseAsync();
    void MarkDirty();
}