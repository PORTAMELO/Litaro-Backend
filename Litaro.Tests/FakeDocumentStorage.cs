using Litaro.Services;

namespace Litaro.Tests;

public sealed class FakeDocumentStorage : IDocumentStorage
{
    public const string DeleteError = "No fue posible eliminar el documento.";

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public Dictionary<string, byte[]> Files { get; } = [];

    public bool IsConfigured { get; set; } = true;

    public bool FailNextUpload { get; set; }

    public bool FailNextDelete { get; set; }

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        if (FailNextUpload)
        {
            FailNextUpload = false;
            throw new InvalidOperationException("No fue posible guardar el documento.");
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Files[path] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(Files[path]));

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (FailNextDelete)
        {
            FailNextDelete = false;
            throw new InvalidOperationException(DeleteError);
        }

        Files.Remove(path);
        return Task.CompletedTask;
    }

    public static DocumentUpload Pdf(string fileName = "plan.pdf", int extraBytes = 100) =>
        Upload(fileName, "application/pdf", [.. "%PDF-1.7\n"u8.ToArray(), .. new byte[extraBytes]]);

    public static DocumentUpload Jpg(string fileName = "foto.jpg") =>
        Upload(fileName, "image/jpeg", [.. JpegSignature, .. new byte[50]]);

    public static DocumentUpload Png(string fileName = "foto.png") =>
        Upload(fileName, "image/png", [.. PngSignature, .. new byte[50]]);

    public static DocumentUpload Upload(string fileName, string contentType, byte[] content) =>
        new(fileName, contentType, content.LongLength, () => new MemoryStream(content));
}
