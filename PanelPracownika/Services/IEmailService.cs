namespace PanelPracownika.Services;

public interface IEmailService
{
    Task SendEmailAsync(
        string to,
        string subject,
        string body
    );

    Task SendEmailWithAttachmentAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file
    );
}