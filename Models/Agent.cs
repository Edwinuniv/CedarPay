using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MoneyTransfer.Models
{
    public enum AgentStatus
    {
        Pending,
        Approved,
        Suspended,
        Rejected
    }
    public class Agent
    {
        public int Id { get; set; }
        [Required]
        public string AgentName { get; set; }
        [Required]
        public string StoreName { get; set; }
        public string? Description { get; set; }
        [Required]
        public string PhoneNumber { get; set; }
        [Required]
        public string Email { get; set; }

        public string? Street { get; set; }
        public string? BuildingName { get; set; }
        public string? BuildingNumber { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string? WorkingHours { get; set; }
        public decimal CommissionRate { get; set; }
        public AgentStatus Status { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [Required]
        public string UserId { get; set; }

        public User User { get; set; }
        public ICollection<Commission> Commissions { get; set; } = new List<Commission>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        [NotMapped]
        public double AverageRating { get; set; }

        [NotMapped]
        public int ReviewCount { get; set; }

        [NotMapped]
        public bool? IsOpenNow
        {
            get
            {
                if (string.IsNullOrEmpty(WorkingHours))
                {
                    return null;
                }
                var dash = WorkingHours.IndexOf('-');
                if (dash < 0)
                {
                    return null;
                }
                var openStr = WorkingHours.Substring(0, dash).Trim();
                var closeStr = WorkingHours.Substring(dash + 1).Trim();
                if (!TimeOnly.TryParse(openStr, out var open))
                {
                    return null;
                }
                if (!TimeOnly.TryParse(closeStr, out var close))
                {
                    return null;
                }
                var now = TimeOnly.FromDateTime(DateTime.Now);
                if (open <= close)
                {
                    return now >= open && now <= close;
                }
                return now >= open || now <= close;
            }
        }

        [NotMapped]
        public string WorkingHoursDisplay => string.IsNullOrEmpty(WorkingHours) ? "Hours not set" : WorkingHours;
    }
}