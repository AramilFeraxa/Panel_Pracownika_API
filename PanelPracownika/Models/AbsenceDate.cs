using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PanelPracownika.Models
{
    public class AbsenceDate
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string Type { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public Login User { get; set; }
    }
}