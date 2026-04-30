using System.ComponentModel.DataAnnotations;
using MoneyTransfer.Models;

namespace MoneyTransfer.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Last name is required")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Father's name is required")]
        public string FatherName { get; set; } = "";

        [Required(ErrorMessage = "Mother's name is required")]
        public string MotherName { get; set; } = "";

        [Required(ErrorMessage = "Date of birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Place of birth is required")]
        public string PlaceOfBirth { get; set; } = "";

        [Required(ErrorMessage = "Gender is required")]
        public string Gender { get; set; } = "";

        [Required(ErrorMessage = "Marital status is required")]
        public string MaritalStatus { get; set; } = "";

        [Required(ErrorMessage = "Nationality is required")]
        public string Nationality { get; set; } = "";

        [Required(ErrorMessage = "Country is required")]
        public string Country { get; set; } = "";

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = "";

        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Account type is required")]
        public string AccountType { get; set; } = "Individual";

        [Required(ErrorMessage = "Occupation is required")]
        public string Occupation { get; set; } = "";

        [Required(ErrorMessage = "Annual income is required")]
        public string AnnualIncomeUSD { get; set; } = "";

        [Required(ErrorMessage = "Record number is required")]
        public string RecordNumber { get; set; } = "";

        [Required(ErrorMessage = "Governate is required")]
        public string Governate { get; set; } = "";

        [Required(ErrorMessage = "District is required")]
        public string District { get; set; } = "";

        [Required(ErrorMessage = "Village is required")]
        public string Village { get; set; } = "";

        [Required(ErrorMessage = "Street is required")]
        public string Street { get; set; } = "";

        [Required(ErrorMessage = "Building name is required")]
        public string BuildingName { get; set; } = "";

        [Required(ErrorMessage = "Building number is required")]
        public string BuildingNumber { get; set; } = "";

        [Required(ErrorMessage = "Region is required")]
        public string Region { get; set; } = "";

        [Required(ErrorMessage = "State is required")]
        public string State { get; set; } = "";

        [Required(ErrorMessage = "Employer name is required")]
        public string EmployerName { get; set; } = "";

        [Required(ErrorMessage = "Document type is required")]
        public DocumentType DocumentType { get; set; }

        [Required(ErrorMessage = "Document number is required")]
        public string DocumentNumber { get; set; } = "";

        public string? UserUsername { get; set; }

        public string? Description { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public bool RemoveProfilePicture { get; set; }
        public bool ProfileCompleted { get; set; }
        public string? PostalCode { get; set; }
        public string? OtherIncomeSource { get; set; }
        public string? OtherNationality { get; set; }

        public string? FrontImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
        public string? KYCStatus { get; set; }

        public string? CompanyName { get; set; }
        public string? BusinessRegNumber { get; set; }
    }
}