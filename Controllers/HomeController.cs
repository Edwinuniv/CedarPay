using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Services.Interfaces;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MoneyTransfer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IEmailService _emailService;

        public HomeController(ILogger<HomeController> logger, IEmailService emailService)
        {
            _logger = logger;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        public IActionResult AboutUs(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult ContactUs(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult Privacy(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult Terms(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult SecurityPolicy(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult Features(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitContact(string fullName, string email, string phone, string subject, string message)
        {
            try
            {
                var supportEmail = "cedarpay.notifications@gmail.com";
                var supportMessage = $@"
                    <h3>New Contact Form Submission</h3>
                    <p><strong>From:</strong> {fullName}</p>
                    <p><strong>Email:</strong> {email}</p>
                    <p><strong>Phone:</strong> {(string.IsNullOrEmpty(phone) ? "Not provided" : phone)}</p>
                    <p><strong>Subject:</strong> {subject}</p>
                    <p><strong>Message:</strong></p>
                    <p>{message}</p>
                    <hr/>
                    <p><strong>IP Address:</strong> {HttpContext.Connection.RemoteIpAddress}</p>
                    <p><strong>Submitted:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                ";

                await _emailService.SendNotificationAsync(supportEmail, "CedarPay Support", $"New Contact: {subject}", supportMessage);

                var autoReplyMessage = $@"
                    <h3>Thank you for contacting CedarPay!</h3>
                    <p>Dear {fullName},</p>
                    <p>We have received your message and will get back to you within 24-48 hours.</p>
                    <br/>
                    <p><strong>Your message summary:</strong></p>
                    <p><strong>Subject:</strong> {subject}</p>
                    <p><strong>Message:</strong> {message}</p>
                    <br/>
                    <p>For urgent matters, please call us at <strong>+961 78 805 447</strong>.</p>
                    <br/>
                    <p>Best regards,<br/>CedarPay Support Team</p>
                ";

                await _emailService.SendNotificationAsync(email, fullName, "We received your message", autoReplyMessage);

                TempData["ContactSuccess"] = "Your message has been sent successfully! We'll get back to you within 24-48 hours.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending contact form");
                TempData["ContactError"] = "Sorry, there was an error sending your message. Please try again later or call us directly at +961 78 805 447.";
            }

            return RedirectToAction("ContactUs");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}