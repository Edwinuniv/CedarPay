using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class KYCController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public KYCController(IUserRepository userRepository, UserManager<User> userManager, ApplicationDbContext context): base(userManager, userRepository)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var kyc = await _context.KYCDocuments.FirstOrDefaultAsync(k => k.UserId == userId);
            ViewBag.KYC = kyc;
            return View(new KYCViewModel
            {
                ExistingStatus = kyc?.Status.ToString(),
                RejectionReason = kyc?.RejectionReason,
                DocumentType = kyc?.DocumentType ?? DocumentType.NationalID,
                DocumentNumber = kyc?.DocumentNumber ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(KYCViewModel vm, IFormFile? frontImage, IFormFile? backImage)
        {
            ModelState.Remove("ExistingStatus");
            ModelState.Remove("RejectionReason");

            var userId = _userManager.GetUserId(User);
            var existing = await _context.KYCDocuments.FirstOrDefaultAsync(k => k.UserId == userId);

            if (existing?.Status == KYCStatus.Approved)
            {
                TempData["Error"] = "Your KYC is already approved.";
                return RedirectToAction("Index");
            }

            if (existing == null && (frontImage == null || frontImage.Length == 0))
                ModelState.AddModelError("", "Please upload the front of your document.");

            if (!ModelState.IsValid)
            {
                ViewBag.KYC = existing;
                return View("Index", vm);
            }

            string? frontUrl = existing?.FrontImageUrl;
            string? backUrl = existing?.BackImageUrl;

            if (frontImage != null && frontImage.Length > 0)
                frontUrl = await SaveKYCImage(frontImage, userId, "front");

            if (backImage != null && backImage.Length > 0)
                backUrl = await SaveKYCImage(backImage, userId, "back");

            if (string.IsNullOrEmpty(frontUrl))
            {
                ModelState.AddModelError("", "Front image is required.");
                ViewBag.KYC = existing;
                return View("Index", vm);
            }

            if (existing != null)
            {
                existing.DocumentType = vm.DocumentType;
                existing.DocumentNumber = vm.DocumentNumber;
                existing.FrontImageUrl = frontUrl;
                existing.BackImageUrl = backUrl; 
                existing.Status = KYCStatus.Pending;
                existing.RejectionReason = null;
                existing.SubmittedAt = DateTime.Now;
                _context.KYCDocuments.Update(existing);
            }
            else
            {
                await _context.KYCDocuments.AddAsync(new KYCDocument
                {
                    DocumentType = vm.DocumentType,
                    DocumentNumber = vm.DocumentNumber,
                    FrontImageUrl = frontUrl,
                    BackImageUrl = backUrl,
                    Status = KYCStatus.Pending,
                    UserId = userId,
                    SubmittedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "KYC documents submitted for review!";
            return RedirectToAction("Index");
        }

        private async Task<string> SaveKYCImage(IFormFile file, string userId, string side)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "kyc");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(file.FileName).ToLower();
            var name = $"{userId}_{side}_{DateTime.Now.Ticks}{ext}";
            var path = Path.Combine(folder, name);
            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/kyc/{name}";
        }
    }
}