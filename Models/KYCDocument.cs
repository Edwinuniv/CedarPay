using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum KYCStatus { Pending, Approved, Rejected }
    public enum DocumentType { Passport, NationalID, DriverLicense }

    public class KYCDocument
    {
        public int Id { get; set; }

        [Required]
        public DocumentType DocumentType { get; set; }

        [Required]
        public string DocumentNumber { get; set; } = "";

        [Required]
        public string FrontImageUrl { get; set; } = "";

        public string? BackImageUrl { get; set; }

        [Required]
        public KYCStatus Status { get; set; }

        public string? RejectionReason { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public User User { get; set; } = null!;
    }
}