using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class ReferralCode
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public User User { get; set; } = null!;

        [Required]
        public string Code { get; set; } = "";

        public int UsageCount { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<ReferralUse> Uses { get; set; } = new List<ReferralUse>();
    }
}