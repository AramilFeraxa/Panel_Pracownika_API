namespace PanelPracownika.Models
{
    public class EmailSettings
    {
        public string MailgunDomain { get; set; } = string.Empty;
        public string MailgunApiKey { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
