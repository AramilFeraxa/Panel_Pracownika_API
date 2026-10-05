namespace PanelPracownika.Models
{
    public class WorkTimePeriodsDto
    {
        public DateTime Date { get; set; }
        public List<WorkPeriodDto> Periods { get; set; } = new();
    }

    public class WorkPeriodDto
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsRemote { get; set; }
    }
}
