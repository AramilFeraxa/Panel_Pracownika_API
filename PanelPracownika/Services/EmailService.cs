using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PanelPracownika.Models;

namespace PanelPracownika.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
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
            "Preparing email. SmtpServerConfigured={SmtpServerConfigured}, Port={Port}, SenderEmail={SenderEmail}, To={To}, ReplyTo={ReplyTo}, SubjectLength={SubjectLength}, HasAttachment={HasAttachment}",
            !string.IsNullOrWhiteSpace(_emailSettings.SmtpServer),
            _emailSettings.Port,
            _emailSettings.SenderEmail,
            to,
            replyTo,
            subject?.Length ?? 0,
            file != null && file.Length > 0
        );

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _emailSettings.SenderName,
            _emailSettings.SenderEmail
        ));

        if (!string.IsNullOrWhiteSpace(replyTo) && !string.Equals(replyTo, _emailSettings.SenderEmail, StringComparison.OrdinalIgnoreCase))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            TextBody = body
        };

        if (file != null && file.Length > 0)
        {
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            builder.Attachments.Add(
                file.FileName,
                memoryStream.ToArray(),
                ContentType.Parse(file.ContentType)
            );
        }

        message.Body = builder.ToMessageBody();

        using var smtp = new MailKit.Net.Smtp.SmtpClient();

        try
        {
            _logger.LogInformation(
                "Connecting to SMTP server {SmtpServer}:{Port} using {SecureSocketOptions}.",
                _emailSettings.SmtpServer,
                _emailSettings.Port,
                SecureSocketOptions.StartTls
            );

            await smtp.ConnectAsync(
                _emailSettings.SmtpServer,
                _emailSettings.Port,
                SecureSocketOptions.StartTls
            );

            _logger.LogInformation("Authenticating to SMTP server as {Username}.", _emailSettings.Username);

            await smtp.AuthenticateAsync(
                _emailSettings.Username,
                _emailSettings.Password
            );

            _logger.LogInformation("Sending email to {To}.", to);

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("SMTP send completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed. Server={SmtpServer}, Port={Port}, To={To}", _emailSettings.SmtpServer, _emailSettings.Port, to);
            throw;
        }
    }
}