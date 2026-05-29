namespace PanelPracownika.Models
{
    public class EmailSettings
    {
        public string SendGridApiKey { get; set; } = string.Empty;
        public string SendGridSenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
