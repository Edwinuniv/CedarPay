using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class ReviewViewModel
    {
        public int Id { get; set; }
        
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string Target { get; set; } = "App";
        public int? AgentId { get; set; }
        public DateTime CreatedAt { get; set; }

        public string UserName { get; set; }
        public double AverageRating { get; set; }
    }
}
