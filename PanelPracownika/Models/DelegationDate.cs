using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PanelPracownika.Models
{
    public class DelegationDate
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public Login User { get; set; }
    }
}