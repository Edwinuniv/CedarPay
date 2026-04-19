using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum ApplicationStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class AgentApplication
    {
        public int Id { get; set; }

        [Required]
        public string AgentName { get; set; }

        [Required]
        public string StoreName { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Email { get; set; }

        public string? Street { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? WorkingHours { get; set; }
        public string? Description { get; set; }

        public ApplicationStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [Required]
        public string UserId { get; set; }  = "";

        public User User { get; set; }  = null;
    }
}