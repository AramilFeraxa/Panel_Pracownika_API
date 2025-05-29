namespace PanelPracownika.Models
{
    public class UpdateSalaryRecordDto
    {
        public double ReceivedAmount { get; set; }
        public bool IsConfirmed { get; set; }
        public bool HasBonus { get; set; }
        public string? Notes { get; set; }
    }

}
