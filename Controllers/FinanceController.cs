using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using ClosedXML.Excel;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class FinanceController : BaseController
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITopUpRepository _topUpRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ApplicationDbContext _context;

        public FinanceController(ITransactionRepository transactionRepository, ITopUpRepository topUpRepository, IWalletRepository walletRepository, ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository): base(userManager, userRepository)
        {
            _transactionRepository = transactionRepository;
            _topUpRepository = topUpRepository;
            _walletRepository = walletRepository;
            _context = context;
        }

        public async Task<IActionResult> Index(int? walletId)
        {
            var userId = _userManager.GetUserId(User);

            var allSent = (await _transactionRepository.GetSentByUserAsync(userId)).ToList();
            var allReceived = (await _transactionRepository.GetReceivedByUserAsync(userId)).ToList();
            var allTopUps = (await _topUpRepository.GetByUserIdAsync(userId)).ToList();
            var wallets = (await _walletRepository.GetByUserIdAsync(userId)).ToList();

            var selectedWallet = walletId.HasValue
                ? wallets.FirstOrDefault(w => w.Id == walletId.Value)
                : wallets.FirstOrDefault(w => w.IsDefault) ?? wallets.FirstOrDefault();

            var sent = selectedWallet != null
                ? allSent.Where(t => t.SenderWalletId == selectedWallet.Id).ToList()
                : allSent;
            var received = selectedWallet != null
                ? allReceived.Where(t => t.ReceiverWalletId == selectedWallet.Id).ToList()
                : allReceived;
            var topUps = selectedWallet != null
                ? allTopUps.Where(t => t.WalletId == selectedWallet.Id).ToList()
                : allTopUps;

            var currSymbol = selectedWallet?.Currency?.Symbol ?? "$";

            var monthly = new List<MonthlyFinanceViewModel>();
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.Now.AddMonths(-i);
                monthly.Add(new MonthlyFinanceViewModel
                {
                    Month = month.ToString("MMM yyyy"),
                    TotalSent = sent.Where(t => t.CreatedAt.Month == month.Month && t.CreatedAt.Year == month.Year).Sum(t => t.Amount),
                    TotalReceived = received.Where(t => t.CreatedAt.Month == month.Month && t.CreatedAt.Year == month.Year).Sum(t => t.ConvertedAmount)
                });
            }

            var totalSentCount = allSent.Count(t => t.Status == TransactionStatus.Completed);
            var nextFreeIn = totalSentCount % 10 == 0 && totalSentCount > 0 ? 0 : 10 - (totalSentCount % 10);

            var fullHistory = new List<FinanceHistoryItemViewModel>();
            foreach (var t in sent)
                fullHistory.Add(new FinanceHistoryItemViewModel { Date = t.CreatedAt, Type = "Sent", Amount = t.Amount, CurrencySymbol = currSymbol, CurrencyCode = selectedWallet?.Currency?.Code ?? "", Description = t.Description ?? "Transfer sent", Reference = t.SerialNumber, Status = t.Status.ToString(), Fee = t.FeeWaived ? 0 : t.FeeAmount, Counterparty = t.ReceiverName ?? "" });
            foreach (var t in received)
                fullHistory.Add(new FinanceHistoryItemViewModel { Date = t.CreatedAt, Type = "Received", Amount = t.ConvertedAmount, CurrencySymbol = currSymbol, CurrencyCode = selectedWallet?.Currency?.Code ?? "", Description = t.Description ?? "Transfer received", Reference = t.SerialNumber, Status = t.Status.ToString(), Fee = 0, Counterparty = "" });
            foreach (var t in topUps)
                fullHistory.Add(new FinanceHistoryItemViewModel { Date = t.CreatedAt, Type = "TopUp", Amount = t.Amount, CurrencySymbol = t.Currency?.Symbol ?? currSymbol, CurrencyCode = t.Currency?.Code ?? "", Description = t.Description ?? "Top-up", Reference = t.PaymentReference ?? "", Status = t.Status.ToString(), Fee = 0, Counterparty = t.Method.ToString() });

            fullHistory = fullHistory.OrderByDescending(x => x.Date).ToList();

            var vm = new FinanceViewModel
            {
                SelectedWalletId = selectedWallet?.Id,
                SelectedWalletCurrencySymbol = currSymbol,
                SelectedWalletCurrencyCode = selectedWallet?.Currency?.Code ?? "",
                TotalSent = sent.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.Amount),
                TotalReceived = received.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.ConvertedAmount),
                TotalTopUps = topUps.Where(t => t.Status == TopUpStatus.Completed).Sum(t => t.Amount),
                TotalFeesPaid = sent.Where(t => !t.FeeWaived).Sum(t => t.FeeAmount),
                TotalFeesSaved = sent.Where(t => t.FeeWaived).Sum(t => t.FeeAmount),
                TransactionCount = totalSentCount,
                FreeTransactionsUsed = allSent.Count(t => t.FeeWaived),
                NextFreeIn = nextFreeIn,
                MonthlyBreakdown = monthly,
                WalletBalances = wallets.Select(w => new WalletSummaryViewModel { Id = w.Id, CurrencyCode = w.Currency?.Code ?? "", CurrencySymbol = w.Currency?.Symbol ?? "", CurrencyName = w.Currency?.Name ?? "", Balance = w.Balance, IsDefault = w.IsDefault, SerialNumber = w.SerialNumber }).ToList(),
                RecentTopUps = topUps.OrderByDescending(t => t.CreatedAt).Take(5).Select(t => new TopUpSummaryViewModel { Amount = t.Amount, Method = t.Method.ToString(), Status = t.Status.ToString(), CreatedAt = t.CreatedAt, CurrencySymbol = t.Currency?.Symbol ?? currSymbol }).ToList(),
                FullHistory = fullHistory
            };

            return View(vm);
        }

        public async Task<IActionResult> ExportExcel(int? walletId)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            var wallets = (await _walletRepository.GetByUserIdAsync(userId)).ToList();
            var selectedWallet = walletId.HasValue
                ? wallets.FirstOrDefault(w => w.Id == walletId.Value)
                : wallets.FirstOrDefault(w => w.IsDefault) ?? wallets.FirstOrDefault();

            var allSent = (await _transactionRepository.GetSentByUserAsync(userId)).ToList();
            var allReceived = (await _transactionRepository.GetReceivedByUserAsync(userId)).ToList();
            var allTopUps = (await _topUpRepository.GetByUserIdAsync(userId)).ToList();

            var sent = selectedWallet != null ? allSent.Where(t => t.SenderWalletId == selectedWallet.Id).ToList() : allSent;
            var received = selectedWallet != null ? allReceived.Where(t => t.ReceiverWalletId == selectedWallet.Id).ToList() : allReceived;
            var topUps = selectedWallet != null ? allTopUps.Where(t => t.WalletId == selectedWallet.Id).ToList() : allTopUps;

            var code = selectedWallet?.Currency?.Code ?? "USD";
            var sym = selectedWallet?.Currency?.Symbol ?? "$";

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

            using var wb = new XLWorkbook();

            var wsSum = wb.Worksheets.Add("Summary");

            wsSum.Cell("A1").Value = "CedarPay Financial Report";
            wsSum.Cell("A1").Style.Font.Bold = true;
            wsSum.Cell("A1").Style.Font.FontSize = 18;
            wsSum.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#00b894");
            wsSum.Cell("A2").Value = $"Wallet: {selectedWallet?.SerialNumber ?? "All"} ({code})";
            wsSum.Cell("A2").Style.Font.Italic = true;
            wsSum.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            wsSum.Cell("A3").Value = $"Account: {user?.FirstName} {user?.LastName}  |  Generated: {DateTime.Now:MMMM dd, yyyy HH:mm}";
            wsSum.Cell("A3").Style.Font.FontColor = XLColor.Gray;
            wsSum.Cell("A3").Style.Font.FontSize = 10;

            wsSum.Cell("A5").Value = "Summary Statistics";
            wsSum.Cell("A5").Style.Font.Bold = true;
            wsSum.Cell("A5").Style.Font.FontSize = 13;
            wsSum.Row(6).Style.Font.Bold = true;
            wsSum.Row(6).Style.Fill.BackgroundColor = XLColor.FromHtml("#00b894");
            wsSum.Row(6).Style.Font.FontColor = XLColor.White;
            wsSum.Cell("A6").Value = "Metric";
            wsSum.Cell("B6").Value = $"Amount ({code})";
            wsSum.Cell("C6").Value = "Notes";

            var kpis = new[]
            {
                ("Total Sent (Completed)", (double)sent.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.Amount), "Outgoing transfers"),
                ("Total Received (Completed)", (double)received.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.ConvertedAmount), "Incoming transfers"),
                ("Total Topped Up", (double)topUps.Where(t => t.Status == TopUpStatus.Completed).Sum(t => t.Amount), "Wallet deposits"),
                ("Fees Paid", (double)sent.Where(t => !t.FeeWaived).Sum(t => t.FeeAmount), "Transfer fees charged"),
                ("Fees Saved (Free Tx)", (double)sent.Where(t => t.FeeWaived).Sum(t => t.FeeAmount), "Fees waived by loyalty program"),
                ("Current Balance", (double)(selectedWallet?.Balance ?? 0), "Live wallet balance"),
                ("Total Transactions", (double)(sent.Count + received.Count), "Sent + received count"),
            };
            for (int i = 0; i < kpis.Length; i++)
            {
                wsSum.Cell(7 + i, 1).Value = kpis[i].Item1;
                wsSum.Cell(7 + i, 2).Value = kpis[i].Item2;
                wsSum.Cell(7 + i, 3).Value = kpis[i].Item3;
                wsSum.Cell(7 + i, 3).Style.Font.FontColor = XLColor.Gray;
                wsSum.Cell(7 + i, 3).Style.Font.Italic = true;
                if (i % 2 == 0)
                    wsSum.Row(7 + i).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
            }
            wsSum.Column("A").Width = 34;
            wsSum.Column("B").Width = 22;
            wsSum.Column("C").Width = 36;

            int chartDataRow = 16;
            wsSum.Cell(chartDataRow, 1).Value = "Month";
            wsSum.Cell(chartDataRow, 2).Value = "Sent";
            wsSum.Cell(chartDataRow, 3).Value = "Received";
            wsSum.Cell(chartDataRow, 4).Value = "Top-Ups";
            wsSum.Row(chartDataRow).Style.Font.Bold = true;
            wsSum.Row(chartDataRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");

            for (int i = 0; i < months.Count; i++)
            {
                wsSum.Cell(chartDataRow + 1 + i, 1).Value = months[i].Label;
                wsSum.Cell(chartDataRow + 1 + i, 2).Value = (double)months[i].Sent;
                wsSum.Cell(chartDataRow + 1 + i, 3).Value = (double)months[i].Received;
                wsSum.Cell(chartDataRow + 1 + i, 4).Value = (double)months[i].TopUp;
            }

            int noteRow = chartDataRow + months.Count + 2;
            wsSum.Cell(noteRow, 1).Value = "💡 To create a chart: select the Monthly Activity table above → Insert → Recommended Charts → Clustered Column";
            wsSum.Cell(noteRow, 1).Style.Font.Italic = true;
            wsSum.Cell(noteRow, 1).Style.Font.FontColor = XLColor.Gray;
            wsSum.Range(noteRow, 1, noteRow, 4).Merge();

            var wsWal = wb.Worksheets.Add("Wallet Balances");
            wsWal.Cell("A1").Value = "Your Wallets";
            wsWal.Cell("A1").Style.Font.Bold = true; wsWal.Cell("A1").Style.Font.FontSize = 14;
            var wh = new[] { "Currency", "Code", "Balance", "Serial Number", "Default", "Active", "Created" };
            for (int i = 0; i < wh.Length; i++) { wsWal.Cell(3, i + 1).Value = wh[i]; wsWal.Cell(3, i + 1).Style.Font.Bold = true; wsWal.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#00b894"); wsWal.Cell(3, i + 1).Style.Font.FontColor = XLColor.White; }
            int r = 4;
            foreach (var w in wallets)
            {
                wsWal.Cell(r, 1).Value = w.Currency?.Name ?? "";
                wsWal.Cell(r, 2).Value = w.Currency?.Code ?? "";
                wsWal.Cell(r, 3).Value = (double)w.Balance;
                wsWal.Cell(r, 4).Value = w.SerialNumber ?? "";
                wsWal.Cell(r, 5).Value = w.IsDefault ? "Yes" : "No";
                wsWal.Cell(r, 6).Value = w.IsActive ? "Active" : "Inactive";
                wsWal.Cell(r, 7).Value = w.CreatedAt.ToString("yyyy-MM-dd");
                if (r % 2 == 0) wsWal.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
                r++;
            }
            wsWal.Columns().AdjustToContents();

            var wsSent = wb.Worksheets.Add("Sent Transactions");
            wsSent.Cell("A1").Value = $"Sent Transactions — {code} Wallet";
            wsSent.Cell("A1").Style.Font.Bold = true; wsSent.Cell("A1").Style.Font.FontSize = 14;
            wsSent.Cell("A2").Value = $"Total: {sent.Count} transactions | {sym}{sent.Sum(t => t.Amount):N2} sent | {sym}{sent.Where(t => !t.FeeWaived).Sum(t => t.FeeAmount):N2} fees paid";
            wsSent.Cell("A2").Style.Font.Italic = true; wsSent.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            var sh = new[] { "Date", "Reference", "Amount", "Currency", "Fee", "Fee Waived", "Recipient", "Description", "Status", "Exchange Rate" };
            for (int i = 0; i < sh.Length; i++) { wsSent.Cell(4, i + 1).Value = sh[i]; wsSent.Cell(4, i + 1).Style.Font.Bold = true; wsSent.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#00b894"); wsSent.Cell(4, i + 1).Style.Font.FontColor = XLColor.White; }
            r = 5;
            foreach (var t in sent.OrderByDescending(t => t.CreatedAt))
            {
                wsSent.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                wsSent.Cell(r, 2).Value = t.SerialNumber;
                wsSent.Cell(r, 3).Value = (double)t.Amount;
                wsSent.Cell(r, 4).Value = t.SenderCurrency?.Code ?? code;
                wsSent.Cell(r, 5).Value = (double)t.FeeAmount;
                wsSent.Cell(r, 6).Value = t.FeeWaived ? "Yes (Free)" : "No";
                wsSent.Cell(r, 7).Value = t.ReceiverName ?? (t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : "");
                wsSent.Cell(r, 8).Value = t.Description ?? "";
                wsSent.Cell(r, 9).Value = t.Status.ToString();
                wsSent.Cell(r, 10).Value = (double)t.ExchangeRateUsed;
                if (r % 2 == 0) wsSent.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
                r++;
            }
            wsSent.Columns().AdjustToContents();

            var wsRec = wb.Worksheets.Add("Received Transactions");
            wsRec.Cell("A1").Value = $"Received Transactions — {code} Wallet";
            wsRec.Cell("A1").Style.Font.Bold = true; wsRec.Cell("A1").Style.Font.FontSize = 14;
            wsRec.Cell("A2").Value = $"Total: {received.Count} transactions | {sym}{received.Sum(t => t.ConvertedAmount):N2} received";
            wsRec.Cell("A2").Style.Font.Italic = true; wsRec.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            var rh = new[] { "Date", "Reference", "Amount Received", "Currency", "Original Amount", "Sender Currency", "Exchange Rate", "Description", "Status" };
            for (int i = 0; i < rh.Length; i++) { wsRec.Cell(4, i + 1).Value = rh[i]; wsRec.Cell(4, i + 1).Style.Font.Bold = true; wsRec.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#00b894"); wsRec.Cell(4, i + 1).Style.Font.FontColor = XLColor.White; }
            r = 5;
            foreach (var t in received.OrderByDescending(t => t.CreatedAt))
            {
                wsRec.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                wsRec.Cell(r, 2).Value = t.SerialNumber;
                wsRec.Cell(r, 3).Value = (double)t.ConvertedAmount;
                wsRec.Cell(r, 4).Value = t.ReceiverCurrency?.Code ?? code;
                wsRec.Cell(r, 5).Value = (double)t.Amount;
                wsRec.Cell(r, 6).Value = t.SenderCurrency?.Code ?? "";
                wsRec.Cell(r, 7).Value = (double)t.ExchangeRateUsed;
                wsRec.Cell(r, 8).Value = t.Description ?? "";
                wsRec.Cell(r, 9).Value = t.Status.ToString();
                if (r % 2 == 0) wsRec.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
                r++;
            }
            wsRec.Columns().AdjustToContents();

            var wsTu = wb.Worksheets.Add("Top Ups");
            wsTu.Cell("A1").Value = $"Top Ups — {code} Wallet";
            wsTu.Cell("A1").Style.Font.Bold = true; wsTu.Cell("A1").Style.Font.FontSize = 14;
            wsTu.Cell("A2").Value = $"Total: {topUps.Count} top-ups | {sym}{topUps.Sum(t => t.Amount):N2} added";
            wsTu.Cell("A2").Style.Font.Italic = true; wsTu.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            var th = new[] { "Date", "Amount", "Currency", "Method", "Reference", "Description", "Status" };
            for (int i = 0; i < th.Length; i++) { wsTu.Cell(4, i + 1).Value = th[i]; wsTu.Cell(4, i + 1).Style.Font.Bold = true; wsTu.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#00b894"); wsTu.Cell(4, i + 1).Style.Font.FontColor = XLColor.White; }
            r = 5;
            foreach (var t in topUps.OrderByDescending(t => t.CreatedAt))
            {
                wsTu.Cell(r, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                wsTu.Cell(r, 2).Value = (double)t.Amount;
                wsTu.Cell(r, 3).Value = t.Currency?.Code ?? code;
                wsTu.Cell(r, 4).Value = t.Method.ToString();
                wsTu.Cell(r, 5).Value = t.PaymentReference ?? "";
                wsTu.Cell(r, 6).Value = t.Description ?? "";
                wsTu.Cell(r, 7).Value = t.Status.ToString();
                if (r % 2 == 0) wsTu.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f7fa");
                r++;
            }
            wsTu.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var filename = $"CedarPay_{code}_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }
    }
}