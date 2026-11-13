using Litaro.Services;

namespace Litaro.Tests;

public sealed class FakeDocumentStorage : IDocumentStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public bool IsConfigured { get; set; } = true;

    public bool FailNextUpload { get; set; }

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
        Files.Remove(path);
        return Task.CompletedTask;
    }

    public static DocumentUpload Pdf(string fileName = "plan.pdf", int extraBytes = 100) =>
        Upload(fileName, "application/pdf", [.. "%PDF-1.7\n"u8.ToArray(), .. new byte[extraBytes]]);

    public static DocumentUpload Upload(string fileName, string contentType, byte[] content) =>
        new(fileName, contentType, content.LongLength, () => new MemoryStream(content));
}
