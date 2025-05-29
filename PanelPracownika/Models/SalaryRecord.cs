namespace PanelPracownika.Models
{
    public class SalaryRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public double ExpectedAmount { get; set; }
        public double ReceivedAmount { get; set; }
        public bool IsConfirmed { get; set; }
        public bool HasBonus { get; set; }
        public string? Notes { get; set; }

        public Login User { get; set; }
    }


}
