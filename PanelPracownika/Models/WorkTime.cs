using System;

namespace PanelPracownika.Models
{
    public class WorkTime
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }    
        public bool IsRemote { get; set; }
        public double Total { get; private set; }

        public int UserId { get; set; }
        public Login User { get; set; }

        public void SetTotal(TimeSpan start, TimeSpan end)
        {
            Total = (end - start).TotalHours;
        }

    }

}
