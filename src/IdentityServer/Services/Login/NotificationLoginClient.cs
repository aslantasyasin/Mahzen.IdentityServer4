using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using IdentityServer.Helper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services.Login;

public sealed class NotificationLoginClient
{
    public const string SendPath = "/api/email/send-internal";
    public const string KeyConfiguration = "Internal:MailApiKey";

    private readonly HttpClient _http;
    private readonly LoginMailerOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationLoginClient> _logger;

    public NotificationLoginClient(HttpClient http, IOptions<LoginMailerOptions> options, IConfiguration configuration, ILogger<NotificationLoginClient> logger)
    {
        _http = http;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogError("login_mail_failed Template={Template} Reason=notification_base_url_missing", templateKey);
            return false;
        }

        var key = _configuration[KeyConfiguration];
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError("login_mail_failed Template={Template} Reason=mail_api_key_missing", templateKey);
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + SendPath)
            {
                Content = JsonContent.Create(new { to, templateKey, model }),
            };
            request.Headers.Add(InternalCallerOnlyAttribute.HeaderName, key);
            using var timeout = new System.Threading.CancellationTokenSource(_options.Timeout);
            using var response = await _http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode) return true;
            _logger.LogError("login_mail_failed Template={Template} Status={Status}", templateKey, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "login_mail_failed Template={Template} Reason=exception", templateKey);
        }
        return false;
    }
}
