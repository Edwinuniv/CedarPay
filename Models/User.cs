using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class User : IdentityUser
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        public string? FatherName { get; set; }
        public string? MotherName { get; set; }
        public string? Description { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? Nationality { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public string? ProfilePictureUrl { get; set; }

        public string? Street { get; set; }
        public string? BuildingName { get; set; }
        public string? BuildingNumber { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? District { get; set; }
        public string? Governate { get; set; }
        public string? Village { get; set; }

        public string? RecordNumber { get; set; }

        public string? Occupation { get; set; }
        public string? EmployerName { get; set; }
        public string? AnnualIncomeUSD { get; set; }
        public string? OtherIncomeSource { get; set; }
        public string? OtherNationality { get; set; }

        public string AccountType { get; set; } = "Individual";

        public string? EmailVerificationCode { get; set; }
        public DateTime? EmailVerificationCodeExpiry { get; set; }

        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        public bool ProfileCompleted { get; set; }
        public int TransactionCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
        public ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Agent> Agents { get; set; } = new List<Agent>();
        public ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
        public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public ICollection<AgentApplication> AgentApplications { get; set; } = new List<AgentApplication>();
        public KYCDocument? KYCDocument { get; set; }
    }
}