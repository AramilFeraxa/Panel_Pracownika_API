namespace PanelPracownika.Models
{
    public class SalaryBreakdown
    {
        public double TotalHours { get; set; }
        public double PrimaryHours { get; set; }
        public double PrimaryHourlyRate { get; set; }
        public double PrimaryAmount { get; set; }
        public double SecondaryMonthlyHours { get; set; }
        public double SecondaryHours { get; set; }
        public double SecondaryHourlyRate { get; set; }
        public double SecondaryAmount { get; set; }
        public double ExpectedAmount { get; set; }
    }
}
