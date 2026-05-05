using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PanelPracownika.Models;
using System.Text;
using System.Text.Json;

namespace PanelPracownika.Services;

public class EmailService : IEmailService
{
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

    public async Task SendEmailWithAttachmentAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file
    )
    {
        _logger.LogInformation(
            "Preparing Mailgun email. DomainConfigured={DomainConfigured}, SenderEmail={SenderEmail}, To={To}, ReplyTo={ReplyTo}, SubjectLength={SubjectLength}, HasAttachment={HasAttachment}",
            !string.IsNullOrWhiteSpace(_emailSettings.MailgunDomain),
            _emailSettings.SenderEmail,
            to,
            replyTo,
            subject?.Length ?? 0,
            file != null && file.Length > 0
        );

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(EmailService));
            client.BaseAddress = new Uri($"https://api.mailgun.net/v3/{_emailSettings.MailgunDomain}/");

            var request = new HttpRequestMessage(HttpMethod.Post, "messages")
            {
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{_emailSettings.MailgunApiKey}"))
                    )
                },
                Content = await BuildMailgunContentAsync(to, replyTo, subject, body, file)
            };

            _logger.LogInformation("Sending Mailgun request to domain {Domain}.", _emailSettings.MailgunDomain);

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mailgun request failed with status {StatusCode}. Response={ResponseBody}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"Mailgun request failed with status {(int)response.StatusCode}: {responseBody}");
            }

            _logger.LogInformation("Mailgun send completed successfully. Response={ResponseBody}", responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mailgun send failed. Domain={Domain}, To={To}", _emailSettings.MailgunDomain, to);
            throw;
        }
    }

    private async Task<MultipartFormDataContent> BuildMailgunContentAsync(string to, string replyTo, string subject, string body, IFormFile file)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent($"{_emailSettings.SenderName} <{_emailSettings.SenderEmail}>", Encoding.UTF8), "from" },
            { new StringContent(to, Encoding.UTF8), "to" },
            { new StringContent(subject, Encoding.UTF8), "subject" },
            { new StringContent(body, Encoding.UTF8), "text" }
        };

        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            content.Add(new StringContent(replyTo, Encoding.UTF8), "h:Reply-To");
        }

        if (file != null && file.Length > 0)
        {
            using var fileStream = file.OpenReadStream();
            await using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);

            var fileContent = new ByteArrayContent(memoryStream.ToArray());
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            }

            content.Add(fileContent, "attachment", file.FileName);
        }

        return content;
    }
}