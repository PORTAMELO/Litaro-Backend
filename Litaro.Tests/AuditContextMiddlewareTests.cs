using System.Net;
using System.Security.Claims;
using Litaro.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Litaro.Tests;

public class AuditContextMiddlewareTests
{
    [Fact]
    public async Task Authenticated_request_sets_user_and_source()
    {
        var http = Request("PUT", "/enrollments/5", "UA");
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "12"),
            new Claim(ClaimTypes.GivenName, "Ana"),
            new Claim(ClaimTypes.Surname, "Pérez"),
        ], "Test"));

        var audit = await RunAsync(http);

        Assert.Equal("USER", audit.ActorKind);
        Assert.Equal(12, audit.UserId);
        Assert.Equal("Ana Pérez", audit.UserName);
        Assert.Equal("PUT /enrollments/5", audit.Source);
        Assert.Equal("10.0.0.9", audit.IpAddress);
        Assert.Equal("UA", audit.UserAgent);
    }

    [Fact]
    public async Task Anonymous_request_is_anonymous()
    {
        var audit = await RunAsync(Request("POST", "/auth/login", "UA"));

        Assert.Equal("ANONYMOUS", audit.ActorKind);
        Assert.Null(audit.UserId);
        Assert.Null(audit.UserName);
        Assert.Equal("POST /auth/login", audit.Source);
    }

    [Fact]
    public async Task Long_values_are_truncated()
    {
        var audit = await RunAsync(Request("GET", "/" + new string('a', 499), new string('u', 1000)));

        Assert.Equal(300, audit.UserAgent!.Length);
        Assert.Equal(200, audit.Source!.Length);
    }

    [Fact]
    public void Truncation_does_not_split_a_surrogate_pair()
    {
        var audit = new AuditContext();

        audit.SetUser(1, new string('a', 199) + "😀");

        Assert.Equal(new string('a', 199), audit.UserName);
    }

    [Fact]
    public void Begin_action_restores_previous_value()
    {
        var audit = new AuditContext();

        using (audit.BeginAction("DOC_REPLACE"))
        {
            Assert.Equal("DOC_REPLACE", audit.Action);
        }

        Assert.Null(audit.Action);
    }

    [Fact]
    public void Set_system_renews_the_operation()
    {
        var audit = new AuditContext();
        var first = audit.OperationId;

        audit.SetSystem("AttendanceNightly");

        Assert.Equal("SYSTEM", audit.ActorKind);
        Assert.Equal("AttendanceNightly", audit.Source);
        Assert.NotEqual(first, audit.OperationId);
    }

    [Fact]
    public async Task Forwarded_for_from_the_proxy_is_the_client_ip()
    {
        var http = Request("POST", "/auth/login", "UA");
        http.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        http.Request.Headers["X-Forwarded-Proto"] = "https";

        var audit = await RunBehindProxyAsync(http);

        Assert.Equal("203.0.113.7", audit.IpAddress);
        Assert.Equal("https", http.Request.Scheme);
    }

    [Fact]
    public async Task Client_supplied_forwarded_for_is_not_trusted()
    {
        var http = Request("POST", "/auth/login", "UA");
        http.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 203.0.113.7";

        var audit = await RunBehindProxyAsync(http);

        Assert.Equal("203.0.113.7", audit.IpAddress);
    }

    [Fact]
    public async Task Without_forwarded_header_the_remote_ip_is_kept()
    {
        var http = Request("POST", "/auth/login", "UA");
        http.Request.Scheme = "http";

        var audit = await RunBehindProxyAsync(http);

        Assert.Equal("10.0.0.9", audit.IpAddress);
        Assert.Equal("http", http.Request.Scheme);
    }

    private static async Task<AuditContext> RunBehindProxyAsync(HttpContext http)
    {
        var options = new ForwardedHeadersOptions();
        ProxyHeaders.Configure(options);
        var audit = new AuditContext();
        var auditMiddleware = new AuditContextMiddleware(_ => Task.CompletedTask);
        var forwarded = new ForwardedHeadersMiddleware(
            context => auditMiddleware.InvokeAsync(context, audit),
            NullLoggerFactory.Instance,
            Options.Create(options));

        await forwarded.Invoke(http);

        return audit;
    }

    private static DefaultHttpContext Request(string method, string path, string userAgent)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.Headers.UserAgent = userAgent;
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");
        return http;
    }

    private static async Task<AuditContext> RunAsync(HttpContext http)
    {
        var audit = new AuditContext();
        var reached = false;
        var middleware = new AuditContextMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, audit);

        Assert.True(reached);
        return audit;
    }
}
