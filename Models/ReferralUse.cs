using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class ReferralUse
    {
        public int Id { get; set; }

        public int ReferralCodeId { get; set; }
        public ReferralCode ReferralCode { get; set; } = null!;

        [Required]
        public string ReferredUserId { get; set; } = "";

        public User ReferredUser { get; set; } = null!;

        public DateTime UsedAt { get; set; }
    }
}
