using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Implementations;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IUserRepository userRepository,
            IAgentApplicationRepository applicationRepository,
            UserManager<User> userManager,
            ApplicationDbContext context,
            IWalletRepository walletRepository,
            ILogger<AccountController> logger)
            : base(userManager, userRepository)
        {
            _applicationRepository = applicationRepository;
            _walletRepository = walletRepository;
            _context = context;
            _logger = logger;
        }
        public async Task<IActionResult> Profile(bool edit = false)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var applications = (await _applicationRepository
                .GetByUserIdAsync(userId))
                .OrderByDescending(a => a.SubmittedAt)
                .ToList();

            ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
            ViewBag.AgentApplicationHistory = applications;

            var approvedWallet = await _context.Wallets
                .AnyAsync(w => w.UserId == userId && w.IsActive);

            ViewBag.WalletApproved = approvedWallet;
            ViewBag.EditMode = edit || !user.ProfileCompleted;

            var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));

            bool isAgent = roles.Contains("Agent");
            bool isAdmin = roles.Contains("Admin");

            ViewBag.IsAgent = isAgent;
            ViewBag.IsAdmin = isAdmin;

            var kyc = await _context.KYCDocuments
                .FirstOrDefaultAsync(k => k.UserId == userId);

            var vm = new ProfileViewModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                FatherName = user.FatherName ?? "",
                MotherName = user.MotherName ?? "",
                DateOfBirth = user.DateOfBirth.Year > 1
                    ? user.DateOfBirth
                    : DateTime.Now.AddYears(-18),
                PlaceOfBirth = user.PlaceOfBirth ?? "",
                Gender = user.Gender ?? "",
                Nationality = user.Nationality ?? "",
                MaritalStatus = user.MaritalStatus ?? "",
                Description = user.Description,
                ProfilePictureUrl = user.ProfilePictureUrl,
                AccountType = user.AccountType ?? "Individual",
                Street = user.Street ?? "",
                BuildingName = user.BuildingName,
                BuildingNumber = user.BuildingNumber,
                City = user.City ?? "",
                Region = user.Region,
                State = user.State,
                Country = user.Country ?? "",
                PostalCode = user.PostalCode,
                District = user.District ?? "",
                Governate = user.Governate ?? "",
                Village = user.Village ?? "",
                RecordNumber = user.RecordNumber ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                Occupation = user.Occupation ?? "",
                EmployerName = user.EmployerName,
                AnnualIncomeUSD = user.AnnualIncomeUSD ?? "",
                OtherIncomeSource = user.OtherIncomeSource,
                OtherNationality = user.OtherNationality,
                ProfileCompleted = user.ProfileCompleted,

                DocumentType = kyc?.DocumentType ?? DocumentType.NationalID,
                DocumentNumber = kyc?.DocumentNumber ?? "",
                FrontImageUrl = kyc?.FrontImageUrl,
                BackImageUrl = kyc?.BackImageUrl,
                KYCStatus = kyc?.Status.ToString(),
                UserUsername = user.UserName
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel vm, IFormFile? profilePicture, IFormFile? frontImage, IFormFile? backImage)
        {
            if (!ModelState.IsValid)
            {
                return View("Profile", vm);
            }

            var userId = _userManager.GetUserId(User);
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Handle cropped picture (base64)
            var croppedPicData = Request.Form["croppedProfilePicture"].FirstOrDefault();

            if (!string.IsNullOrEmpty(croppedPicData) && croppedPicData.StartsWith("data:image"))
            {
                try
                {
                    var base64Data = croppedPicData.Split(',')[1];
                    var imageBytes = Convert.FromBase64String(base64Data);

                    var uploadsFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot", "uploads", "profiles");
                    Directory.CreateDirectory(uploadsFolder);

                    DeleteFile(user.ProfilePictureUrl);

                    var fileName = $"{userId}_{DateTime.Now.Ticks}.jpg";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    user.ProfilePictureUrl = $"/uploads/profiles/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cropped image");
                    ModelState.AddModelError("", "Failed to process cropped image.");
                    return View("Profile", vm);
                }
            }
            else if (vm.RemoveProfilePicture)
            {
                DeleteFile(user.ProfilePictureUrl);
                user.ProfilePictureUrl = null;
            }
            else if (profilePicture != null && profilePicture.Length > 0)
            {
                var result = await SaveUploadedFile(
                    profilePicture, userId, "profiles");
                if (result.Error != null)
                {
                    ModelState.AddModelError("", result.Error);
                    return View("Profile", vm);
                }
                DeleteFile(user.ProfilePictureUrl);
                user.ProfilePictureUrl = result.Url;
            }

            user.FirstName = vm.FirstName;
            user.LastName = vm.LastName;
            user.FatherName = vm.FatherName;
            user.MotherName = vm.MotherName;
            user.DateOfBirth = vm.DateOfBirth;
            user.PlaceOfBirth = vm.PlaceOfBirth;
            user.Gender = vm.Gender;
            user.Nationality = vm.Nationality;
            user.MaritalStatus = vm.MaritalStatus;
            user.Description = vm.Description;
            user.AccountType = vm.AccountType;
            user.PhoneNumber = vm.PhoneNumber;
            user.Street = vm.Street;
            user.BuildingName = vm.BuildingName;
            user.BuildingNumber = vm.BuildingNumber;
            user.City = vm.City;
            user.Region = vm.Region;
            user.State = vm.State;
            user.Country = vm.Country;
            user.PostalCode = vm.PostalCode;
            user.District = vm.District;
            user.Governate = vm.Governate;
            user.Village = vm.Village;
            user.RecordNumber = vm.RecordNumber;
            user.Occupation = vm.Occupation;
            user.EmployerName = vm.EmployerName;
            user.AnnualIncomeUSD = vm.AnnualIncomeUSD;
            user.OtherIncomeSource = vm.OtherIncomeSource;
            user.OtherNationality = vm.OtherNationality;
            user.ProfileCompleted = true;

            await _userRepository.UpdateAsync(user);

            if (!string.IsNullOrEmpty(vm.UserUsername))
            {
                var cleanUsername = vm.UserUsername.ToLower().Trim();

                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserName == cleanUsername &&
                        u.Id != userId);

                if (existingUser != null)
                {
                    ModelState.AddModelError("", $"Username '@{cleanUsername}' is already taken.");
                    return View("Profile", vm);
                }

                // Update username
                var userToUpdate = await _userManager.FindByIdAsync(userId);
                if (userToUpdate != null)
                {
                    await _userManager.SetUserNameAsync(userToUpdate, cleanUsername);
                    user.UserName = cleanUsername;
                    await _userRepository.UpdateAsync(user);
                }
            }

            // FIX: KYC Update with proper EF tracking handling
            if (!string.IsNullOrEmpty(vm.DocumentNumber))
            {
                // Use a fresh query without tracking issues
                var existingKyc = await _context.KYCDocuments
                    .FirstOrDefaultAsync(k => k.UserId == userId);

                string? frontUrl = existingKyc?.FrontImageUrl;
                string? backUrl = existingKyc?.BackImageUrl;

                if (frontImage != null && frontImage.Length > 0)
                {
                    var r = await SaveUploadedFile(
                        frontImage, userId + "_front", "kyc");
                    if (r.Url != null) frontUrl = r.Url;
                }

                if (backImage != null && backImage.Length > 0)
                {
                    var r = await SaveUploadedFile(
                        backImage, userId + "_back", "kyc");
                    if (r.Url != null) backUrl = r.Url;
                }

                if (existingKyc != null)
                {
                    // Only update if not already approved
                    if (existingKyc.Status != KYCStatus.Approved)
                    {
                        // Directly update the entity
                        existingKyc.DocumentType = vm.DocumentType;
                        existingKyc.DocumentNumber = vm.DocumentNumber;
                        existingKyc.FrontImageUrl = frontUrl;
                        existingKyc.BackImageUrl = backUrl;
                        existingKyc.Status = KYCStatus.Pending;
                        existingKyc.RejectionReason = null;
                        existingKyc.SubmittedAt = DateTime.Now;

                        // Detach any existing tracked entity first to avoid tracking conflicts
                        var tracked = _context.ChangeTracker.Entries<KYCDocument>()
                            .FirstOrDefault(e => e.Entity.Id == existingKyc.Id);
                        if (tracked != null)
                            tracked.State = EntityState.Detached;

                        _context.KYCDocuments.Update(existingKyc);
                    }
                }
                else
                {
                    var newKyc = new KYCDocument
                    {
                        DocumentType = vm.DocumentType,
                        DocumentNumber = vm.DocumentNumber,
                        FrontImageUrl = frontUrl,
                        BackImageUrl = backUrl,
                        Status = KYCStatus.Pending,
                        UserId = userId,
                        SubmittedAt = DateTime.Now
                    };
                    await _context.KYCDocuments.AddAsync(newKyc);
                }

                await _context.SaveChangesAsync();
            }

            ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
            ViewBag.FullName = $"{user.FirstName} {user.LastName}";
            ViewBag.UserInitials = $"{user.FirstName?.Substring(0, 1)}{user.LastName?.Substring(0, 1)}";

            TempData["Success"] = "Profile saved successfully!";

            return RedirectToAction("Profile");
        }

        private async Task<(string? Url, string? Error)> SaveUploadedFile(
            IFormFile file, string userId, string folder)
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowed.Contains(ext))
            {
                return (null, "Only JPG, PNG and GIF allowed.");
            }
            if (file.Length > 2 * 1024 * 1024)
            {
                return (null, "Image must be under 2MB.");
            }
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
            Directory.CreateDirectory(dir);

            var name = $"{userId}_{DateTime.Now.Ticks}{ext}";
            var path = Path.Combine(dir, name);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return ($"/uploads/{folder}/{name}", null);
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                url.TrimStart('/'));
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);

            }
        }
        public async Task<IActionResult> DeleteAccount()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDeleteAccount()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var picPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePictureUrl.TrimStart('/'));
                if (System.IO.File.Exists(picPath))
                {
                    System.IO.File.Delete(picPath);
                }
            }

            await HttpContext.SignOutAsync();

            await _userManager.DeleteAsync(user);

            return RedirectToPage("/Account/Register", new { area = "Identity" });
        }

        public async Task<IActionResult> Report()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userRepository.GetByIdAsync(userId);

            var sent = await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Where(t => t.SenderWallet != null &&
                            t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Where(t => t.ReceiverWallet != null &&
                            t.ReceiverWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var wallets = await _walletRepository
                .GetByUserIdAsync(userId);

            ViewBag.User = user;
            ViewBag.Sent = sent;
            ViewBag.Received = received;
            ViewBag.Wallets = wallets.ToList();
            ViewBag.GeneratedAt = DateTime.Now;

            return View();
        }

        public IActionResult SetupChoice()
        {
            return View();
        }
    }
}