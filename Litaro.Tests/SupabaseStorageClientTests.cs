using System.Net;
using Litaro.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Litaro.Tests;

public class SupabaseStorageClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string Body { get; set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }

    private static SupabaseStorageOptions Bind(Dictionary<string, string?> values)
    {
        var options = new SupabaseStorageOptions();
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("SupabaseStorage").Bind(options);
        return options;
    }

    private static (SupabaseStorageClient Client, RecordingHandler Handler) Create(string key)
    {
        var handler = new RecordingHandler();
        var options = new SupabaseStorageOptions { Url = "https://demo.supabase.co/", ServiceRoleKey = key, BucketName = "litaro-documents" };
        return (new SupabaseStorageClient(new HttpClient(handler), Options.Create(options)), handler);
    }

    [Fact]
    public void Binds_service_role_key_and_bucket_name()
    {
        var options = Bind(new()
        {
            ["SupabaseStorage:Url"] = "https://demo.supabase.co",
            ["SupabaseStorage:ServiceRoleKey"] = "sb_secret_abc",
            ["SupabaseStorage:BucketName"] = "litaro-documents",
        });

        var client = new SupabaseStorageClient(new HttpClient(), Options.Create(options));

        Assert.True(client.IsConfigured);
    }

    [Fact]
    public async Task Secret_api_key_goes_only_in_apikey_header()
    {
        var (client, handler) = Create("sb_secret_abc");

        await client.UploadAsync("Grades/x.pdf", new MemoryStream([1]), "application/pdf");

        Assert.Equal("https://demo.supabase.co/storage/v1/object/litaro-documents/Grades/x.pdf", handler.Last!.RequestUri!.ToString());
        Assert.Equal("sb_secret_abc", handler.Last.Headers.GetValues("apikey").Single());
        Assert.Null(handler.Last.Headers.Authorization);
    }

    [Fact]
    public async Task Legacy_jwt_key_is_also_sent_as_bearer()
    {
        var (client, handler) = Create("eyJhbGciOiJIUzI1NiJ9.e30.sig");

        await client.UploadAsync("Grades/x.pdf", new MemoryStream([1]), "application/pdf");

        Assert.Equal("Bearer", handler.Last!.Headers.Authorization!.Scheme);
        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.e30.sig", handler.Last.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task Delete_throws_on_server_error()
    {
        var (client, handler) = Create("sb_secret_abc");
        handler.Status = HttpStatusCode.InternalServerError;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteAsync("Grades/x.pdf"));

        Assert.Equal("No fue posible eliminar el documento.", error.Message);
        Assert.Equal(HttpMethod.Delete, handler.Last!.Method);
    }

    [Fact]
    public async Task Delete_treats_missing_object_as_deleted()
    {
        var (client, handler) = Create("sb_secret_abc");

        handler.Status = HttpStatusCode.NotFound;
        await client.DeleteAsync("Grades/x.pdf");

        handler.Status = HttpStatusCode.BadRequest;
        handler.Body = "{\"statusCode\":\"404\",\"error\":\"not_found\",\"message\":\"Object not found\"}";
        await client.DeleteAsync("Grades/x.pdf");

        handler.Body = "{\"error\":\"invalid_request\"}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteAsync("Grades/x.pdf"));
    }
}
