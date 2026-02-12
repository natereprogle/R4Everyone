using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4DatabaseValidationTests
{
    [Fact]
    public async Task ValidateDatabaseAsync_ThrowsWhenStreamIsNotSeekable()
    {
        await using var stream = new NonSeekableReadStream(new byte[0x100]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => R4Database.ValidateDatabaseAsync(stream));
    }

    [Fact]
    public async Task ValidateDatabaseAsync_ThrowsForSmallHeader()
    {
        await using var stream = new MemoryStream(new byte[12]);

        await Assert.ThrowsAsync<FileLoadException>(() => R4Database.ValidateDatabaseAsync(stream));
    }

    [Fact]
    public async Task ValidateDatabaseAsync_ThrowsForInvalidMagicString()
    {
        var header = new byte[0x100];
        var encodingBytes = R4EncodingHelper.GetBytes(R4Encoding.UTF8);
        Array.Copy(encodingBytes, 0, header, 0x4C, 4);

        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<FileLoadException>(() => R4Database.ValidateDatabaseAsync(stream));
    }

    [Fact]
    public async Task ValidateDatabaseAsync_ThrowsForInvalidEncodingMarker()
    {
        var header = new byte[0x100];
        "R4 CheatCode"u8.ToArray().CopyTo(header, 0);

        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<FileLoadException>(() => R4Database.ValidateDatabaseAsync(stream));
    }

    [Fact]
    public async Task ValidateDatabaseAsync_AllowsValidHeader()
    {
        var header = new byte[0x100];
        "R4 CheatCode"u8.ToArray().CopyTo(header, 0);
        var encodingBytes = R4EncodingHelper.GetBytes(R4Encoding.SJIS);
        Array.Copy(encodingBytes, 0, header, 0x4C, 4);

        await using var stream = new MemoryStream(header);

        await R4Database.ValidateDatabaseAsync(stream);
    }

    private sealed class NonSeekableReadStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin loc)
        {
            throw new NotSupportedException();
        }
    }
}
