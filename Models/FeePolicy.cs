using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class FeePolicy
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public decimal FeePercentage { get; set; }
        public decimal FixedFee { get; set; }
        public int FreeTransactionThreshold { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
