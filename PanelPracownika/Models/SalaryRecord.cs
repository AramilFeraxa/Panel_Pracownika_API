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

        [System.Text.Json.Serialization.JsonIgnore]
        public string? CalculationDetails { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public SalaryBreakdown? Breakdown
        {
            get => CalculationDetails == null ? null : System.Text.Json.JsonSerializer.Deserialize<SalaryBreakdown>(CalculationDetails);
            set => CalculationDetails = value == null ? null : System.Text.Json.JsonSerializer.Serialize(value);
        }

        public Login User { get; set; }
    }


}
