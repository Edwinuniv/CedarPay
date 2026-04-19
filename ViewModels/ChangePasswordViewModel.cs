using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword",
            ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}