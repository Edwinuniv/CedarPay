using System.ComponentModel.DataAnnotations;
using MoneyTransfer.Models;

namespace MoneyTransfer.ViewModels
{
    public class KYCViewModel
    {
        [Required(ErrorMessage = "Document type is required")]
        public DocumentType DocumentType { get; set; }

        [Required(ErrorMessage = "Document number is required")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "Document number must be 4-50 characters")]
        public string DocumentNumber { get; set; }

        public string? ExistingStatus { get; set; }
        public string? RejectionReason { get; set; }
    }
}