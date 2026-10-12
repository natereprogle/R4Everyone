using Microsoft.JSInterop;

namespace R4Everyone.Web.Services;

/// <summary>
/// Saves generated files to the user's device through the browser's download mechanism.
/// </summary>
public sealed class FileDownloadService(IJSRuntime js)
{
    public ValueTask DownloadAsync(string fileName, byte[] content, string contentType = "application/octet-stream") =>
        js.InvokeVoidAsync("r4eFiles.download", fileName, contentType, content);
}
