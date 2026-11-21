using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Services.Login;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityServer.Tests;

public class NotificationLoginClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    private static (NotificationLoginClient Client, RecordingHandler Handler) Create(string? mailKey, string? sharedKey = "paylasimli")
    {
        var handler = new RecordingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Internal:ApiKey"] = sharedKey,
            [NotificationLoginClient.KeyConfiguration] = mailKey,
        }).Build();
        var options = Options.Create(new LoginMailerOptions { BaseUrl = "http://notification" });
        return (new NotificationLoginClient(new HttpClient(handler), options, config, NullLogger<NotificationLoginClient>.Instance), handler);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_the_mail_key_nothing_is_sent_even_if_the_shared_key_exists(string? mailKey)
    {
        var (client, handler) = Create(mailKey);

        Assert.False(await client.SendAsync("ali@ornek.com", LoginMailTemplates.Code, new Dictionary<string, string> { ["Code"] = "123456" }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_mail_key_not_the_shared_key_is_sent()
    {
        var (client, handler) = Create("posta-anahtari");

        Assert.True(await client.SendAsync("ali@ornek.com", LoginMailTemplates.Code, new Dictionary<string, string> { ["Code"] = "123456" }));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("http://notification" + NotificationLoginClient.SendPath), request.RequestUri);
        Assert.Equal(new[] { "posta-anahtari" }, request.Headers.GetValues("X-Internal-Key"));
    }
}
