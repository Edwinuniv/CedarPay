using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class AnnouncementController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementController(ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository)
            : base(userManager, userRepository)
        { _context = context; }

        // ── PUBLIC: all logged-in users see active announcements ──────
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var list = await _context.Announcements
                .Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
            return View(list);
        }

        // ── ADMIN CRUD ────────────────────────────────────────────────
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var all = await _context.Announcements.Include(a => a.CreatedBy)
                .OrderByDescending(a => a.CreatedAt).ToListAsync();
            var cutoff = DateTime.Now.AddMinutes(-15);
            ViewBag.OnlineUsers = await _context.Users.CountAsync(u => u.LastLoginAt != null && u.LastLoginAt >= cutoff);
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            return View(all);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View(new Announcement { IsActive = true, ShowToAll = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Announcement vm)
        {
            ModelState.Remove("CreatedByUserId"); ModelState.Remove("CreatedBy");
            if (!ModelState.IsValid) return View(vm);
            vm.CreatedByUserId = _userManager.GetUserId(User);
            vm.CreatedAt = DateTime.Now;
            _context.Announcements.Add(vm);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Announcement published!";
            return RedirectToAction(nameof(Manage));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var a = await _context.Announcements.Include(x => x.CreatedBy).FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound();
            return View(a);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _context.Announcements.FindAsync(id);
            if (a == null) return NotFound();
            return View(a);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Announcement vm)
        {
            ModelState.Remove("CreatedByUserId"); ModelState.Remove("CreatedBy");
            if (!ModelState.IsValid) return View(vm);
            var ex = await _context.Announcements.FindAsync(vm.Id);
            if (ex == null) return NotFound();
            ex.Title = vm.Title; ex.Content = vm.Content; ex.Type = vm.Type;
            ex.IsActive = vm.IsActive; ex.ShowToAll = vm.ShowToAll; ex.ExpiresAt = vm.ExpiresAt;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Updated!";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _context.Announcements.FindAsync(id);
            if (a != null) { _context.Announcements.Remove(a); await _context.SaveChangesAsync(); }
            TempData["Success"] = "Deleted.";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Toggle(int id)
        {
            var a = await _context.Announcements.FindAsync(id);
            if (a != null) { a.IsActive = !a.IsActive; await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Manage));
        }

        // ── JSON endpoint for Layout banner ──────────────────────────
        [AllowAnonymous]
        public async Task<IActionResult> Active()
        {
            var now = DateTime.Now;
            var list = await _context.Announcements
                .Where(a => a.IsActive && a.ShowToAll && (a.ExpiresAt == null || a.ExpiresAt > now))
                .OrderByDescending(a => a.CreatedAt).Take(3)
                .Select(a => new { a.Id, a.Title, a.Content, Type = a.Type.ToString() })
                .ToListAsync();
            return Json(list);
        }
    }
}
