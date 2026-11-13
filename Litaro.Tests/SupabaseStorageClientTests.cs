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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
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
}
