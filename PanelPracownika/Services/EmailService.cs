using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PanelPracownika.Models;
using System.Net.Mail;

namespace PanelPracownika.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public async Task SendEmailWithAttachmentAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file
    )
    {
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

        await smtp.ConnectAsync(
            _emailSettings.SmtpServer,
            _emailSettings.Port,
            SecureSocketOptions.StartTls
        );

        await smtp.AuthenticateAsync(
            _emailSettings.Username,
            _emailSettings.Password
        );

        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}