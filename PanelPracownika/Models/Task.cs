using System;

namespace PanelPracownika.Models
{
    public class UserTask
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public DateTime? DueDate { get; set; }
        public bool Completed { get; set; }
        public int UserId { get; set; }
    }

}
