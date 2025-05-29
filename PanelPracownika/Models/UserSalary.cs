namespace PanelPracownika.Models
{
    public class UserSalary
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ContractType { get; set; }
        public double? HourlyRate { get; set; }
        public double? MonthlySalary { get; set; }

        public Login User { get; set; }
    }

}
