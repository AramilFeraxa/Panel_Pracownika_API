namespace PanelPracownika.Services;

public interface IEmailService
{
    Task SendEmailWithAttachmentAsync(
        string to,
        string replyTo,
        string subject,
        string body,
        IFormFile file
    );
}