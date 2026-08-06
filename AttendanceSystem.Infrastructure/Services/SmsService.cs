using System.Net.Http.Headers;
using System.Text;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Logging;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// SMS over a configurable HTTP gateway.
///
/// Driven by a URL, a method and a request template rather than a vendor SDK. The gateways
/// used in this market are all plain HTTP that disagree only about parameter names, so a
/// template covers them and switching provider stays a settings change.
/// </summary>
public class SmsService : ISmsService
{
    private readonly AttendanceDbContext _db;
    private readonly ISecretProtector _secrets;
    private readonly IHttpClientFactory _http;

    public SmsService(AttendanceDbContext db, ISecretProtector secrets, IHttpClientFactory http)
    {
        _db = db;
        _secrets = secrets;
        _http = http;
    }

    public Task<Result> SendTestSmsAsync(string toNumber) =>
        SendAsync(toNumber,
            "Test message from your Attendance Management System. "
          + "If you received this, SMS sending is working.");

    public async Task<Result> SendAsync(string toNumber, string message)
    {
        var s = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();

        if (s == null || !s.SmsEnabled)
            return Result.Failure("SMS sending is switched off in Settings.");

        if (string.IsNullOrWhiteSpace(s.SmsApiUrl))
            return Result.Failure("No SMS gateway is configured. Set it under Settings → SMS.");

        if (string.IsNullOrWhiteSpace(toNumber))
            return Result.Failure("No mobile number to send to.");

        var apiKey = _secrets.Unprotect(s.SmsApiKeyEncrypted) ?? string.Empty;

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var isPost = string.Equals(s.SmsHttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
            var isJson = s.SmsContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

            // Escaped for the format it is being placed into. A message containing a quote or
            // a brace would otherwise produce malformed JSON, and one containing '&' would
            // silently truncate a query string.
            var body = Substitute(s.SmsRequestTemplate, toNumber, message, apiKey, s.SmsSenderId,
                                  encodeForUrl: !isPost || !isJson,
                                  escapeForJson: isPost && isJson);

            var url = s.SmsApiUrl.Trim();
            HttpRequestMessage request;

            if (isPost)
            {
                request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body ?? string.Empty, Encoding.UTF8,
                        isJson ? "application/json" : "application/x-www-form-urlencoded")
                };
            }
            else
            {
                var query = string.IsNullOrWhiteSpace(body) ? string.Empty
                          : (url.Contains('?') ? "&" : "?") + body;
                request = new HttpRequestMessage(HttpMethod.Get, url + query);
            }

            if (!string.IsNullOrWhiteSpace(s.SmsAuthHeader))
            {
                var header = Substitute(s.SmsAuthHeader, toNumber, message, apiKey, s.SmsSenderId,
                                        encodeForUrl: false, escapeForJson: false)!;

                // Parsed when it looks like "Scheme value", set raw otherwise — some gateways
                // expect a bare token with no scheme at all.
                var parts = header.Split(' ', 2);
                request.Headers.Authorization = parts.Length == 2
                    ? new AuthenticationHeaderValue(parts[0], parts[1])
                    : new AuthenticationHeaderValue(header);
            }

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"SmsService.SendAsync to {toNumber}: {(int)response.StatusCode} {responseBody}",
                    new InvalidOperationException(responseBody));

                // The gateway's own words are returned. "Insufficient credit" and "invalid
                // sender id" are both 400s, and only the body distinguishes them.
                return Result.Failure(
                    $"The SMS gateway returned {(int)response.StatusCode}: " +
                    Trim(responseBody, 400));
            }

            return Result.Success();
        }
        catch (TaskCanceledException)
        {
            return Result.Failure("The SMS gateway did not respond within 30 seconds.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"SmsService.SendAsync to {toNumber}", ex);
            return Result.Failure($"Could not reach the SMS gateway: {ex.Message}");
        }
    }

    /// <summary>
    /// Fills the placeholders in a template, escaping each value for the format it lands in.
    /// </summary>
    private static string? Substitute(string? template, string to, string message, string apiKey,
                                      string? sender, bool encodeForUrl, bool escapeForJson)
    {
        if (string.IsNullOrWhiteSpace(template)) return template;

        string Prepare(string value) =>
              encodeForUrl  ? Uri.EscapeDataString(value)
            : escapeForJson ? JsonEscape(value)
            : value;

        return template
            .Replace("{to}", Prepare(to))
            .Replace("{message}", Prepare(message))
            .Replace("{apikey}", Prepare(apiKey))
            .Replace("{sender}", Prepare(sender ?? string.Empty));
    }

    /// <summary>Escapes a value for embedding inside a JSON string literal.</summary>
    private static string JsonEscape(string value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return json[1..^1]; // strip the surrounding quotes the serialiser adds
    }

    private static string Trim(string value, int max) =>
        string.IsNullOrEmpty(value) ? "(no detail)"
        : value.Length <= max ? value
        : value[..max] + "…";
}
