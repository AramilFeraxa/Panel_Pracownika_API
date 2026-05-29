using Microsoft.Extensions.Options;
using PanelPracownika.Models;
using System.Text;
using System.Text.Json;

namespace PanelPracownika.Services;

public class EmailService : IEmailService
{
    private const string SendGridEndpoint = "https://api.sendgrid.com/v3/mail/send";
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        await SendMessageAsync(to, null, subject, body, null);
    }

    public async Task SendEmailWithAttachmentAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file
    )
    {
        await SendMessageAsync(to, replyTo, subject, body, file);
    }

    private async Task SendMessageAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file)
    {
        var fromAddress = string.IsNullOrWhiteSpace(_emailSettings.FromEmail)
            ? _emailSettings.SendGridSenderEmail
            : _emailSettings.FromEmail;

        _logger.LogInformation(
            "Preparing SendGrid email. SenderConfigured={SenderConfigured}, FromAddress={FromAddress}, SenderEmail={SenderEmail}, To={To}, ReplyTo={ReplyTo}, SubjectLength={SubjectLength}, HasAttachment={HasAttachment}",
            !string.IsNullOrWhiteSpace(_emailSettings.SendGridApiKey),
            fromAddress,
            _emailSettings.SendGridSenderEmail,
            to,
            replyTo,
            subject?.Length ?? 0,
            file != null && file.Length > 0
        );

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(EmailService));
            var request = new HttpRequestMessage(HttpMethod.Post, SendGridEndpoint)
            {
                Headers =
                {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _emailSettings.SendGridApiKey)
                },
                Content = await BuildSendGridContentAsync(fromAddress, to, replyTo, subject, body, file)
            };

            _logger.LogInformation("Sending SendGrid request.");

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SendGrid request failed with status {StatusCode}. Response={ResponseBody}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"SendGrid request failed with status {(int)response.StatusCode}: {responseBody}");
            }

            _logger.LogInformation("SendGrid send completed successfully. Response={ResponseBody}", responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid send failed. To={To}", to);
            throw;
        }
    }

    private Task<StringContent> BuildSendGridContentAsync(string fromAddress, string to, string replyTo, string subject, string body, IFormFile file)
    {
        var payload = new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[] { new { email = to } },
                    subject
                }
            },
            from = new { email = fromAddress, name = _emailSettings.SenderName },
            reply_to = string.IsNullOrWhiteSpace(replyTo) ? null : new { email = replyTo },
            content = new[]
            {
                new
                {
                    type = "text/plain",
                    value = body
                }
            }
        };

        return Task.FromResult(new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    }
}