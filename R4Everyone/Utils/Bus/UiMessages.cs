using R4Everyone.Binary4Everyone;

namespace R4Everyone.Utils.Bus;

public sealed record OpenFile;

public sealed record FileOpened(string FullPath, R4Database? Database);

public sealed record SelectionChange(object selection);

public sealed record FileClosed;

public sealed record FileSaved(string? FullPath);