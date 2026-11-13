using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Litaro.Services;

public interface IDocumentStorage
{
    bool IsConfigured { get; }

    Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    Task DeleteAsync(string path, CancellationToken ct = default);
}

public class SupabaseStorageOptions
{
    public string Url { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public string BucketName { get; set; } = "";
}

public class SupabaseStorageClient(HttpClient http, IOptions<SupabaseStorageOptions> options) : IDocumentStorage
{
    private const string SecretKeyPrefix = "sb_";

    private readonly SupabaseStorageOptions settings = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(settings.Url) &&
        !string.IsNullOrWhiteSpace(settings.ServiceRoleKey) &&
        !string.IsNullOrWhiteSpace(settings.BucketName);

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Headers.Add("x-upsert", "false");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("No fue posible guardar el documento.");
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get, path);
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new InvalidOperationException("No fue posible leer el documento.");
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Delete, path);
        using var response = await http.SendAsync(request, ct);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var url = $"{settings.Url.TrimEnd('/')}/storage/v1/object/{Uri.EscapeDataString(settings.BucketName)}/{path}";
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", settings.ServiceRoleKey);

        if (!settings.ServiceRoleKey.StartsWith(SecretKeyPrefix, StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ServiceRoleKey);

        return request;
    }
}
