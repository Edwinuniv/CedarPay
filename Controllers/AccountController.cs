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

        public AccountController(IUserRepository userRepository, IAgentApplicationRepository applicationRepository, UserManager<User> userManager, ApplicationDbContext context, IWalletRepository walletRepository, ILogger<AccountController> logger): base(userManager, userRepository)
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

            var applications = (await _applicationRepository.GetByUserIdAsync(userId)).OrderByDescending(a => a.SubmittedAt).ToList();

            ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
            ViewBag.AgentApplicationHistory = applications;

            var approvedWallet = await _context.Wallets.AnyAsync(w => w.UserId == userId && w.IsActive);

            ViewBag.WalletApproved = approvedWallet;
            ViewBag.EditMode = edit || !user.ProfileCompleted;

            var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
            bool isAgent = roles.Contains("Agent");
            bool isAdmin = roles.Contains("Admin");

            ViewBag.IsAgent = isAgent;
            ViewBag.IsAdmin = isAdmin;

            var kyc = await _context.KYCDocuments.FirstOrDefaultAsync(k => k.UserId == userId);

            var vm = new ProfileViewModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                FatherName = user.FatherName ?? "",
                MotherName = user.MotherName ?? "",
                DateOfBirth = user.DateOfBirth.Year > 1 ? user.DateOfBirth : DateTime.Now.AddYears(-18),
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
                KYCStatus = kyc?.Status.ToString(),
                UserUsername = user.UserName
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel vm, IFormFile? profilePicture)
        {
            ModelState.Remove("DocumentType");
            ModelState.Remove("DocumentNumber");

            if (!ModelState.IsValid)
            {
                var applications = (await _applicationRepository.GetByUserIdAsync(_userManager.GetUserId(User))).OrderByDescending(a => a.SubmittedAt).ToList();
                ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                ViewBag.AgentApplicationHistory = applications;
                ViewBag.EditMode = true;
                var userId = _userManager.GetUserId(User);
                var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(userId));
                ViewBag.IsAgent = roles.Contains("Agent");
                ViewBag.IsAdmin = roles.Contains("Admin");
                return View("Profile", vm);
            }

            var userIdForUpdate = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userIdForUpdate);
            if (user == null)
            {
                return NotFound();
            }
            var normalizedPhone = vm.PhoneNumber?.Trim();
            if (!string.IsNullOrEmpty(normalizedPhone))
            {
                var phoneOwner = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone && u.Id != userIdForUpdate);
                if (phoneOwner != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already registered to another account.");
                    var applications = (await _applicationRepository.GetByUserIdAsync(userIdForUpdate)).OrderByDescending(a => a.SubmittedAt).ToList();
                    ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                    ViewBag.AgentApplicationHistory = applications;
                    ViewBag.EditMode = true;
                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.IsAgent = roles.Contains("Agent");
                    ViewBag.IsAdmin = roles.Contains("Admin");
                    return View("Profile", vm);
                }
            }

            var croppedPicData = Request.Form["croppedProfilePicture"].FirstOrDefault();

            if (!string.IsNullOrEmpty(croppedPicData) && croppedPicData.StartsWith("data:image"))
            {
                try
                {
                    var base64Data = croppedPicData.Split(',')[1];
                    var imageBytes = Convert.FromBase64String(base64Data);

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                    Directory.CreateDirectory(uploadsFolder);

                    DeleteFile(user.ProfilePictureUrl);

                    var fileName = $"{userIdForUpdate}_{DateTime.Now.Ticks}.jpg";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    user.ProfilePictureUrl = $"/uploads/profiles/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cropped image");
                    ModelState.AddModelError("", "Failed to process cropped image.");
                    var applications = (await _applicationRepository.GetByUserIdAsync(userIdForUpdate)).OrderByDescending(a => a.SubmittedAt).ToList();
                    ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                    ViewBag.AgentApplicationHistory = applications;
                    ViewBag.EditMode = true;
                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.IsAgent = roles.Contains("Agent");
                    ViewBag.IsAdmin = roles.Contains("Admin");
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
                var result = await SaveUploadedFile(profilePicture, userIdForUpdate, "profiles");
                if (result.Error != null)
                {
                    ModelState.AddModelError("", result.Error);
                    var applications = (await _applicationRepository.GetByUserIdAsync(userIdForUpdate)).OrderByDescending(a => a.SubmittedAt).ToList();
                    ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                    ViewBag.AgentApplicationHistory = applications;
                    ViewBag.EditMode = true;
                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.IsAgent = roles.Contains("Agent");
                    ViewBag.IsAdmin = roles.Contains("Admin");
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
            user.PhoneNumber = normalizedPhone;
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

            if (!string.IsNullOrEmpty(vm.UserUsername))
            {
                var cleanUsername = vm.UserUsername.ToLower().Trim();
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == cleanUsername && u.Id != userIdForUpdate);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", $"Username '@{cleanUsername}' is already taken.");
                    var applications = (await _applicationRepository.GetByUserIdAsync(userIdForUpdate)).OrderByDescending(a => a.SubmittedAt).ToList();
                    ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                    ViewBag.AgentApplicationHistory = applications;
                    ViewBag.EditMode = true;
                    var roles = await _userManager.GetRolesAsync(user);
                    ViewBag.IsAgent = roles.Contains("Agent");
                    ViewBag.IsAdmin = roles.Contains("Admin");
                    return View("Profile", vm);
                }
                await _userManager.SetUserNameAsync(user, cleanUsername);
            }

            var identityResult = await _userManager.UpdateAsync(user);
            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                var applications = (await _applicationRepository.GetByUserIdAsync(userIdForUpdate)).OrderByDescending(a => a.SubmittedAt).ToList();
                ViewBag.AgentApplicationStatus = applications.FirstOrDefault()?.Status.ToString();
                ViewBag.AgentApplicationHistory = applications;
                ViewBag.EditMode = true;
                var roles = await _userManager.GetRolesAsync(user);
                ViewBag.IsAgent = roles.Contains("Agent");
                ViewBag.IsAdmin = roles.Contains("Admin");
                return View("Profile", vm);
            }

            TempData["Success"] = "Profile saved successfully!";
            return RedirectToAction("Profile");
        }

        private async Task<(string? Url, string? Error)> SaveUploadedFile(IFormFile file, string userId, string folder)
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
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", url.TrimStart('/'));
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
                    System.IO.File.Delete(picPath);
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
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Where(t => t.SenderWallet != null && t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Where(t => t.ReceiverWallet != null && t.ReceiverWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var topUps = await _context.TopUps
                .Include(t => t.Currency)
                .Include(t => t.Wallet)
                .Where(t => t.Wallet != null && t.Wallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            ViewBag.User = user;
            ViewBag.Sent = sent;
            ViewBag.Received = received;
            ViewBag.TopUps = topUps;
            ViewBag.Wallets = wallets.ToList();
            ViewBag.GeneratedAt = DateTime.Now;

            return View();
        }

        public async Task<IActionResult> ExportExcel()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userRepository.GetByIdAsync(userId);

            var sent = await _context.Transactions
                .Include(t => t.SenderCurrency).Include(t => t.ReceiverCurrency)
                .Include(t => t.SenderWallet)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Where(t => t.SenderWallet != null && t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderCurrency).Include(t => t.ReceiverCurrency)
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet)
                .Where(t => t.ReceiverWallet != null && t.ReceiverWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

            var topUps = await _context.TopUps
                .Include(t => t.Currency).Include(t => t.Wallet)
                .Where(t => t.Wallet != null && t.Wallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

            var wallets = (await _walletRepository.GetByUserIdAsync(userId)).ToList();

            var months = new List<(string Label, decimal Sent, decimal Received, decimal TopUp)>();
            for (int i = 5; i >= 0; i--)
            {
                var m = DateTime.Now.AddMonths(-i);
                months.Add((
                    m.ToString("MMM yyyy"),
                    sent.Where(t => t.CreatedAt.Month == m.Month && t.CreatedAt.Year == m.Year).Sum(t => t.Amount),
                    received.Where(t => t.CreatedAt.Month == m.Month && t.CreatedAt.Year == m.Year).Sum(t => t.ConvertedAmount),
                    topUps.Where(t => t.CreatedAt.Month == m.Month && t.CreatedAt.Year == m.Year).Sum(t => t.Amount)
                ));
            }

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var wsSum = wb.Worksheets.Add("Account Summary");
            wsSum.Cell("A1").Value = "CedarPay — Full Account Report";
            wsSum.Cell("A1").Style.Font.Bold = true; wsSum.Cell("A1").Style.Font.FontSize = 18;
            wsSum.Cell("A1").Style.Font.FontColor = ClosedXML.Excel.XLColor.FromHtml("#00b894");
            wsSum.Cell("A2").Value = $"Account: {user?.FirstName} {user?.LastName}  |  {user?.Email}";
            wsSum.Cell("A2").Style.Font.Italic = true; wsSum.Cell("A2").Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;
            wsSum.Cell("A3").Value = $"Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}  |  KYC: {(user?.IsVerified == true ? "Verified" : "Pending")}";
            wsSum.Cell("A3").Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray; wsSum.Cell("A3").Style.Font.FontSize = 10;
            wsSum.Cell("A5").Value = "Account KPIs (All Wallets Combined)";
            wsSum.Cell("A5").Style.Font.Bold = true; wsSum.Cell("A5").Style.Font.FontSize = 13;
            wsSum.Row(6).Style.Font.Bold = true;
            wsSum.Row(6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894");
            wsSum.Row(6).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            wsSum.Cell("A6").Value = "Metric"; wsSum.Cell("B6").Value = "Value";
            var kpis = new[]
            {
                ("Full Name", $"{user?.FirstName} {user?.LastName}"),
                ("Email", user?.Email ?? ""),
                ("Phone", user?.PhoneNumber ?? "—"),
                ("Account Type", user?.AccountType ?? "Individual"),
                ("Nationality", user?.Nationality ?? "—"),
                ("Joined", user?.CreatedAt.ToString("MMM dd, yyyy") ?? ""),
                ("KYC Status", user?.IsVerified == true ? "Verified ✓" : "Pending"),
                ("Total Wallets", wallets.Count.ToString()),
                ("Total Sent (USD equiv.)", $"${sent.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.Amount):N2}"),
                ("Total Received (USD equiv.)", $"${received.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.ConvertedAmount):N2}"),
                ("Total Topped Up", $"${topUps.Where(t => t.Status == TopUpStatus.Completed).Sum(t => t.Amount):N2}"),
                ("Fees Paid", $"${sent.Where(t => !t.FeeWaived).Sum(t => t.FeeAmount):N2}"),
                ("Fees Saved (Free Tx)", $"${sent.Where(t => t.FeeWaived).Sum(t => t.FeeAmount):N2}"),
                ("Total Transactions", (sent.Count + received.Count).ToString()),
            };
            for (int i = 0; i < kpis.Length; i++)
            {
                wsSum.Cell(7 + i, 1).Value = kpis[i].Item1;
                wsSum.Cell(7 + i, 2).Value = kpis[i].Item2;
                if (i % 2 == 0) wsSum.Row(7 + i).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa");
            }
            wsSum.Column("A").Width = 34; wsSum.Column("B").Width = 36;
            int cdr = 23;
            wsSum.Cell(cdr, 1).Value = "Month"; wsSum.Cell(cdr, 2).Value = "Sent"; wsSum.Cell(cdr, 3).Value = "Received"; wsSum.Cell(cdr, 4).Value = "Top-Ups";
            wsSum.Row(cdr).Style.Font.Bold = true; wsSum.Row(cdr).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa");
            for (int i = 0; i < months.Count; i++)
            {
                wsSum.Cell(cdr + 1 + i, 1).Value = months[i].Label;
                wsSum.Cell(cdr + 1 + i, 2).Value = (double)months[i].Sent;
                wsSum.Cell(cdr + 1 + i, 3).Value = (double)months[i].Received;
                wsSum.Cell(cdr + 1 + i, 4).Value = (double)months[i].TopUp;
            }
            int noteRow = cdr + months.Count + 2;
            wsSum.Cell(noteRow, 1).Value = "💡 To create a chart: select the Monthly Activity table above → Insert → Recommended Charts → Clustered Column";
            wsSum.Cell(noteRow, 1).Style.Font.Italic = true; wsSum.Cell(noteRow, 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;
            wsSum.Range(noteRow, 1, noteRow, 4).Merge();

            var wsWal = wb.Worksheets.Add("Wallet Balances");
            wsWal.Cell("A1").Value = "Wallet Balances"; wsWal.Cell("A1").Style.Font.Bold = true; wsWal.Cell("A1").Style.Font.FontSize = 14;
            var wh = new[] { "Currency Name", "Code", "Symbol", "Balance", "Serial Number", "Default", "Active", "Created" };
            for (int i = 0; i < wh.Length; i++) { wsWal.Cell(3, i + 1).Value = wh[i]; wsWal.Cell(3, i + 1).Style.Font.Bold = true; wsWal.Cell(3, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894"); wsWal.Cell(3, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
            int r = 4;
            foreach (var w in wallets) { wsWal.Cell(r, 1).Value = w.Currency?.Name ?? ""; wsWal.Cell(r, 2).Value = w.Currency?.Code ?? ""; wsWal.Cell(r, 3).Value = w.Currency?.Symbol ?? ""; wsWal.Cell(r, 4).Value = (double)w.Balance; wsWal.Cell(r, 5).Value = w.SerialNumber ?? ""; wsWal.Cell(r, 6).Value = w.IsDefault ? "Yes" : "No"; wsWal.Cell(r, 7).Value = w.IsActive ? "Active" : "Inactive"; wsWal.Cell(r, 8).Value = w.CreatedAt.ToString("yyyy-MM-dd"); if (r % 2 == 0) wsWal.Row(r).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa"); r++; }
            wsWal.Columns().AdjustToContents();

            var wsSent = wb.Worksheets.Add("Sent Transactions");
            wsSent.Cell("A1").Value = "Sent Transactions — All Wallets"; wsSent.Cell("A1").Style.Font.Bold = true; wsSent.Cell("A1").Style.Font.FontSize = 14;
            wsSent.Cell("A2").Value = $"Total: {sent.Count} | Fees paid: ${sent.Where(t => !t.FeeWaived).Sum(t => t.FeeAmount):N2} | Saved: ${sent.Where(t => t.FeeWaived).Sum(t => t.FeeAmount):N2}";
            wsSent.Cell("A2").Style.Font.Italic = true; wsSent.Cell("A2").Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;
            var sh = new[] { "Date", "Reference", "Amount", "Currency", "Converted Amount", "Receiver Currency", "Exchange Rate", "Fee", "Fee Waived", "Recipient", "Description", "Status" };
            for (int i = 0; i < sh.Length; i++) { wsSent.Cell(4, i + 1).Value = sh[i]; wsSent.Cell(4, i + 1).Style.Font.Bold = true; wsSent.Cell(4, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894"); wsSent.Cell(4, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
            r = 5;
            foreach (var t in sent) { wsSent.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"); wsSent.Cell(r, 2).Value = t.SerialNumber; wsSent.Cell(r, 3).Value = (double)t.Amount; wsSent.Cell(r, 4).Value = t.SenderCurrency?.Code ?? ""; wsSent.Cell(r, 5).Value = (double)t.ConvertedAmount; wsSent.Cell(r, 6).Value = t.ReceiverCurrency?.Code ?? ""; wsSent.Cell(r, 7).Value = (double)t.ExchangeRateUsed; wsSent.Cell(r, 8).Value = (double)t.FeeAmount; wsSent.Cell(r, 9).Value = t.FeeWaived ? "Yes" : "No"; wsSent.Cell(r, 10).Value = t.ReceiverName ?? (t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : ""); wsSent.Cell(r, 11).Value = t.Description ?? ""; wsSent.Cell(r, 12).Value = t.Status.ToString(); if (r % 2 == 0) wsSent.Row(r).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa"); r++; }
            wsSent.Columns().AdjustToContents();

            var wsRec = wb.Worksheets.Add("Received Transactions");
            wsRec.Cell("A1").Value = "Received Transactions — All Wallets"; wsRec.Cell("A1").Style.Font.Bold = true; wsRec.Cell("A1").Style.Font.FontSize = 14;
            wsRec.Cell("A2").Value = $"Total: {received.Count} | ${received.Sum(t => t.ConvertedAmount):N2} received";
            wsRec.Cell("A2").Style.Font.Italic = true; wsRec.Cell("A2").Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;
            var rh = new[] { "Date", "Reference", "Amount Received", "Currency", "Original Amount", "Sender Currency", "Exchange Rate", "Sender", "Description", "Status" };
            for (int i = 0; i < rh.Length; i++) { wsRec.Cell(4, i + 1).Value = rh[i]; wsRec.Cell(4, i + 1).Style.Font.Bold = true; wsRec.Cell(4, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894"); wsRec.Cell(4, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
            r = 5;
            foreach (var t in received) { wsRec.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"); wsRec.Cell(r, 2).Value = t.SerialNumber; wsRec.Cell(r, 3).Value = (double)t.ConvertedAmount; wsRec.Cell(r, 4).Value = t.ReceiverCurrency?.Code ?? ""; wsRec.Cell(r, 5).Value = (double)t.Amount; wsRec.Cell(r, 6).Value = t.SenderCurrency?.Code ?? ""; wsRec.Cell(r, 7).Value = (double)t.ExchangeRateUsed; wsRec.Cell(r, 8).Value = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : ""; wsRec.Cell(r, 9).Value = t.Description ?? ""; wsRec.Cell(r, 10).Value = t.Status.ToString(); if (r % 2 == 0) wsRec.Row(r).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa"); r++; }
            wsRec.Columns().AdjustToContents();

            var wsTu = wb.Worksheets.Add("Top Ups");
            wsTu.Cell("A1").Value = "Top Ups — All Wallets"; wsTu.Cell("A1").Style.Font.Bold = true; wsTu.Cell("A1").Style.Font.FontSize = 14;
            wsTu.Cell("A2").Value = $"Total: {topUps.Count} | ${topUps.Sum(t => t.Amount):N2} added";
            wsTu.Cell("A2").Style.Font.Italic = true; wsTu.Cell("A2").Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;
            var th = new[] { "Date", "Amount", "Currency", "Wallet Serial", "Method", "Reference", "Description", "Status" };
            for (int i = 0; i < th.Length; i++) { wsTu.Cell(4, i + 1).Value = th[i]; wsTu.Cell(4, i + 1).Style.Font.Bold = true; wsTu.Cell(4, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894"); wsTu.Cell(4, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White; }
            r = 5;
            foreach (var t in topUps) { wsTu.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"); wsTu.Cell(r, 2).Value = (double)t.Amount; wsTu.Cell(r, 3).Value = t.Currency?.Code ?? ""; wsTu.Cell(r, 4).Value = t.Wallet?.SerialNumber ?? ""; wsTu.Cell(r, 5).Value = t.Method.ToString(); wsTu.Cell(r, 6).Value = t.PaymentReference ?? ""; wsTu.Cell(r, 7).Value = t.Description ?? ""; wsTu.Cell(r, 8).Value = t.Status.ToString(); if (r % 2 == 0) wsTu.Row(r).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f5f7fa"); r++; }
            wsTu.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var filename = $"CedarPay_FullReport_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        public IActionResult SetupChoice()
        {
            var userId = _userManager.GetUserId(User);
            var user = _userManager.FindByIdAsync(userId).Result;
            if (user != null && user.ProfileCompleted)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }
    }
}