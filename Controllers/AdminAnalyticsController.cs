using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminAnalyticsController> _logger;

        public AdminAnalyticsController(ApplicationDbContext context, ILogger<AdminAnalyticsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);

            var vm = new AnalyticsDashboardViewModel
            {
                AverageAppRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.App && r.User != null)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
                TotalAppReviews = await _context.Reviews.CountAsync(r => r.Target == ReviewTarget.App),
                AppRatingDistribution = await GetRatingDistribution(ReviewTarget.App),

                AverageAgentRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.Agent && r.Agent != null)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
                TotalAgentReviews = await _context.Reviews.CountAsync(r => r.Target == ReviewTarget.Agent),
                TopRatedAgents = await GetTopRatedAgents(10),

                AverageTransactionRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.Transaction && r.Transaction != null)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
                TotalTransactionReviews = await _context.Reviews.CountAsync(r => r.Target == ReviewTarget.Transaction),

                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsersThisMonth = await _context.Users
                    .CountAsync(u => u.LastLoginAt >= startOfMonth),
                NewUsersThisMonth = await _context.Users
                    .CountAsync(u => u.CreatedAt >= startOfMonth),

                TotalTransactions = await _context.Transactions.CountAsync(),
                TotalTransactionsThisMonth = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= startOfMonth),
                TotalVolumeThisMonth = await _context.Transactions
                    .Where(t => t.CreatedAt >= startOfMonth)
                    .SumAsync(t => t.Amount),
                TotalVolumeYear = await _context.Transactions
                    .Where(t => t.CreatedAt >= startOfYear)
                    .SumAsync(t => t.Amount),
                TotalFeesCollected = await _context.Transactions
                    .Where(t => !t.FeeWaived)
                    .SumAsync(t => t.FeeAmount),
                TotalFreeTransactions = await _context.Transactions
                    .CountAsync(t => t.FeeWaived),

                MonthlyStats = await GetMonthlyStats(12),

                RecentReviews = await _context.Reviews
                    .Include(r => r.User)
                    .Include(r => r.Agent)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(20)
                    .ToListAsync(),
                RecentTransactions = await _context.Transactions
                    .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                    .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(20)
                    .ToListAsync(),
                RecentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(20)
                    .ToListAsync(),
            };

            return View(vm);
        }

        public async Task<IActionResult> ReviewsAnalytics()
        {
            var vm = new ReviewsAnalyticsViewModel
            {
                AppReviews = await _context.Reviews
                    .Include(r => r.User)
                    .Where(r => r.Target == ReviewTarget.App)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(),
                AgentReviews = await _context.Reviews
                    .Include(r => r.User)
                    .Include(r => r.Agent)
                    .Where(r => r.Target == ReviewTarget.Agent && r.Agent != null)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(),
                TransactionReviews = await _context.Reviews
                    .Include(r => r.User)
                    .Include(r => r.Transaction)
                    .Where(r => r.Target == ReviewTarget.Transaction && r.Transaction != null)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(),
                TopAgents = await GetTopRatedAgents(20),
                AverageAppRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.App)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
                AverageAgentRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.Agent && r.Agent != null)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
                AverageTransactionRating = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.Transaction && r.Transaction != null)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,
            };

            return View(vm);
        }

        public async Task<IActionResult> MonthlyReport(int? year = null, int? month = null)
        {
            var now = DateTime.Now;
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;

            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1);

            var vm = new MonthlyReportViewModel
            {
                Year = targetYear,
                Month = targetMonth,
                MonthName = startDate.ToString("MMMM yyyy"),

                NewUsers = await _context.Users
                    .CountAsync(u => u.CreatedAt >= startDate && u.CreatedAt < endDate),
                ActiveUsers = await _context.Users
                    .CountAsync(u => u.LastLoginAt >= startDate && u.LastLoginAt < endDate),
                TotalUsers = await _context.Users.CountAsync(),

                TotalTransactions = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= startDate && t.CreatedAt < endDate),
                TotalVolume = await _context.Transactions
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate)
                    .SumAsync(t => t.Amount),
                TotalFees = await _context.Transactions
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate && !t.FeeWaived)
                    .SumAsync(t => t.FeeAmount),
                FreeTransactions = await _context.Transactions
                    .CountAsync(t => t.CreatedAt >= startDate && t.CreatedAt < endDate && t.FeeWaived),

                NewAppReviews = await _context.Reviews
                    .CountAsync(r => r.Target == ReviewTarget.App && r.CreatedAt >= startDate && r.CreatedAt < endDate),
                NewAgentReviews = await _context.Reviews
                    .CountAsync(r => r.Target == ReviewTarget.Agent && r.CreatedAt >= startDate && r.CreatedAt < endDate),
                NewTransactionReviews = await _context.Reviews
                    .CountAsync(r => r.Target == ReviewTarget.Transaction && r.CreatedAt >= startDate && r.CreatedAt < endDate),
                AverageRatingThisMonth = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.App && r.CreatedAt >= startDate && r.CreatedAt < endDate)
                    .AverageAsync(r => (double?)r.Rating) ?? 0,

                TopAgents = await _context.Reviews
                    .Where(r => r.Target == ReviewTarget.Agent && r.Agent != null && r.CreatedAt >= startDate && r.CreatedAt < endDate)
                    .GroupBy(r => r.AgentId)
                    .Select(g => new TopAgentViewModel
                    {
                        AgentId = g.Key ?? 0,
                        StoreName = g.First().Agent!.StoreName,
                        AgentName = g.First().Agent!.AgentName,
                        AverageRating = g.Average(r => r.Rating),
                        ReviewCount = g.Count()
                    })
                    .OrderByDescending(a => a.AverageRating)
                    .Take(10)
                    .ToListAsync(),

                DailyBreakdown = await GetDailyStats(startDate, endDate),
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportMonthlyReport(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var stats = new
            {
                Period = $"{startDate:MMMM yyyy}",
                NewUsers = await _context.Users.CountAsync(u => u.CreatedAt >= startDate && u.CreatedAt < endDate),
                Transactions = await _context.Transactions.CountAsync(t => t.CreatedAt >= startDate && t.CreatedAt < endDate),
                Volume = await _context.Transactions.Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate).SumAsync(t => t.Amount),
                Reviews = await _context.Reviews.CountAsync(r => r.CreatedAt >= startDate && r.CreatedAt < endDate),
            };

            return Json(stats);
        }

        private async Task<Dictionary<int, int>> GetRatingDistribution(ReviewTarget target)
        {
            var distribution = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                var count = await _context.Reviews
                    .CountAsync(r => r.Target == target && r.Rating == i);
                distribution[i] = count;
            }
            return distribution;
        }

        private async Task<List<TopAgentViewModel>> GetTopRatedAgents(int count)
        {
            return await _context.Reviews
                .Where(r => r.Target == ReviewTarget.Agent && r.Agent != null)
                .GroupBy(r => r.AgentId)
                .Select(g => new TopAgentViewModel
                {
                    AgentId = g.Key ?? 0,
                    StoreName = g.First().Agent!.StoreName,
                    AgentName = g.First().Agent!.AgentName,
                    AverageRating = g.Average(r => r.Rating),
                    ReviewCount = g.Count()
                })
                .OrderByDescending(a => a.AverageRating)
                .Take(count)
                .ToListAsync();
        }

        private async Task<List<MonthlyStatViewModel>> GetMonthlyStats(int months)
        {
            var stats = new List<MonthlyStatViewModel>();
            for (int i = months - 1; i >= 0; i--)
            {
                var monthDate = DateTime.Now.AddMonths(-i);
                var startDate = new DateTime(monthDate.Year, monthDate.Month, 1);
                var endDate = startDate.AddMonths(1);

                stats.Add(new MonthlyStatViewModel
                {
                    Month = startDate.ToString("MMM yyyy"),
                    Transactions = await _context.Transactions
                        .CountAsync(t => t.CreatedAt >= startDate && t.CreatedAt < endDate),
                    Volume = await _context.Transactions
                        .Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate)
                        .SumAsync(t => t.Amount),
                    NewUsers = await _context.Users
                        .CountAsync(u => u.CreatedAt >= startDate && u.CreatedAt < endDate),
                    Reviews = await _context.Reviews
                        .CountAsync(r => r.CreatedAt >= startDate && r.CreatedAt < endDate),
                    AverageRating = await _context.Reviews
                        .Where(r => r.Target == ReviewTarget.App && r.CreatedAt >= startDate && r.CreatedAt < endDate)
                        .AverageAsync(r => (double?)r.Rating) ?? 0
                });
            }
            return stats;
        }

        private async Task<List<DailyBreakdownViewModel>> GetDailyStats(DateTime startDate, DateTime endDate)
        {
            var stats = new List<DailyBreakdownViewModel>();
            for (var date = startDate; date < endDate; date = date.AddDays(1))
            {
                var nextDay = date.AddDays(1);
                stats.Add(new DailyBreakdownViewModel
                {
                    Date = date,
                    Transactions = await _context.Transactions
                        .CountAsync(t => t.CreatedAt >= date && t.CreatedAt < nextDay),
                    Volume = await _context.Transactions
                        .Where(t => t.CreatedAt >= date && t.CreatedAt < nextDay)
                        .SumAsync(t => t.Amount),
                    NewUsers = await _context.Users
                        .CountAsync(u => u.CreatedAt >= date && u.CreatedAt < nextDay),
                    Reviews = await _context.Reviews
                        .CountAsync(r => r.CreatedAt >= date && r.CreatedAt < nextDay)
                });
            }
            return stats;
        }
    }
}