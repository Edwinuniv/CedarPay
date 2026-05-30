# 🌲 CedarPay • Lebanon's Digital Wallet Platform

<div align="center">

![CedarPay](https://img.shields.io/badge/CedarPay-Lebanon's%20Digital%20Wallet-00b894?style=for-the-badge)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-512BD4?style=for-the-badge&logo=dotnet)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**CedarPay** is a full-featured digital wallet and money transfer platform built for Lebanon, supporting multi-currency wallets, agent cash networks, KYC verification, real-time communication, analytics, and AI-powered support.

</div>

---

## 📋 Table of Contents

- [Project Highlights](#project-highlights)
- [Project Statistics](#project-statistics)
- [My Role](#my-role)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Skills Demonstrated](#skills-demonstrated)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [User Roles](#user-roles)
- [API Endpoints](#api-endpoints)
- [Configuration](#configuration)
- [Database Schema](#database-schema)
- [Architecture Overview](#architecture-overview)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

---

## Project Highlights

• Full-stack financial platform built with ASP.NET Core 9  
• Multi-currency wallet ecosystem  
• Real-time SignalR messaging  
• AI-powered Cedar Bot using Google Gemini  
• KYC verification workflow  
• Agent-based cash network  
• Interactive analytics dashboard  
• Role-based security architecture  
• REST API integration  
• Responsive modern UI  

---

## Project Statistics

• 30+ Entity Models  
• 20+ Controllers  
• 100+ Razor Views  
• Multi-Currency Support (10 currencies)  
• SignalR Real-Time Communication  
• Google Gemini Integration  
• Role-Based Authorization  
• Stripe Payment Processing  
• Agent Cash Network  
• Analytics & Reporting System  

---

## My Role

This project was designed and developed entirely by me as a full-stack software engineering project.

**Responsibilities included:**

• System architecture design  
• Database modeling (30+ entities)  
• Backend development (20+ controllers)  
• Frontend UI/UX development (100+ views)  
• Authentication & authorization (ASP.NET Core Identity)  
• REST API development  
• Google Gemini AI integration  
• SignalR real-time chat implementation  
• Stripe payment processing  
• Reporting and analytics (Excel export, charts)  
• KYC verification workflow  
• Agent commission system  

---

## Features

### 💸 Core Banking

• **Multi-Currency Wallets** • USD, EUR, LBP, AED, GBP, SAR, TRY, EGP, JOD, KWD  
• **Instant Transfers** • Wallet-to-wallet and mobile number transfers with live currency conversion  
• **Top-Up** • Credit card payments via Stripe and bank transfers  
• **Loyalty Program** • Every 10th transaction is completely fee-free 🎉  
• **Scheduled Transfers** • Recurring payments (daily, weekly, monthly)  
• **Transaction Receipts** • Printable and shareable PDF receipts  

### 🏪 Agent Network

• **Cash In / Cash Out** • Authorized agents handle physical cash operations  
• **Agent Map** • Interactive Leaflet map with real-time nearest-agent routing  
• **Commission System** • Configurable per-agent commission rates  
• **Agent Dashboard** • Revenue, commission history, and cash flow analytics  
• **Multi-Store Management** • Agents can manage multiple store locations  

### 🛡️ Security & Compliance

• **KYC Verification** • Document upload and admin review workflow  
• **ASP.NET Core Identity** • Role-based access control (Admin / Agent / User)  
• **Email Confirmation** • 6-digit OTP verification on registration  
• **Account Lockout** • Automatic lockout after failed login attempts  
• **Google OAuth** • Sign in with Google support  
• **Activity Logs** • Full audit trail of user actions  

### 🤖 AI & Communication

• **Cedar Bot** • Gemini AI-powered chatbot with live account context  
• **Real-Time Chat** • SignalR-powered messaging with reactions, replies, file sharing, and voice messages  
• **Email Notifications** • Transactional emails for all account events via Gmail SMTP  
• **In-App Notifications** • Real-time push notification badges and toasts  
• **Announcement System** • Admin broadcast system with expiry support  

### 📊 Analytics & Reporting

• **Admin Analytics Dashboard** • Platform-wide KPIs, charts, and user activity  
• **Monthly Reports** • Daily breakdowns with Chart.js visualizations  
• **Excel Exports** • One-click export of users, transactions, and commissions via ClosedXML  
• **Finance Overview** • Per-wallet revenue/spending charts with 6-month history  
• **Earnings Report** • Platform fees and agent commission breakdowns  

### 👤 User Experience

• **Beneficiary Management** • Save contacts by @username or wallet serial  
• **Reviews & Ratings** • Rate the app, agents, and individual transactions  
• **Dark Mode** • Full dark/light theme toggle with localStorage persistence  
• **Referral Program** • Shareable referral codes with friend tracking  
• **Responsive Design** • Mobile-first layout with collapsible sidebar  

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Framework** | ASP.NET Core 9 (MVC + Razor Pages) |
| **Database** | SQL Server + Entity Framework Core |
| **Authentication** | ASP.NET Core Identity + Google OAuth |
| **Real-Time** | SignalR |
| **Payments** | Stripe |
| **AI** | Google Gemini API |
| **Email** | MailKit / SMTP |
| **Maps** | Leaflet.js + OpenStreetMap |
| **Charts** | Chart.js |
| **Excel** | ClosedXML |
| **Icons** | Bootstrap Icons |
| **CSS Framework** | Bootstrap 5 + Custom Design System |

---

## Skills Demonstrated

• ASP.NET Core MVC  
• Entity Framework Core  
• SQL Server Design & Optimization  
• REST API Development  
• SignalR Real-Time Applications  
• Authentication & Authorization (Identity, OAuth)  
• Google Gemini AI Integration  
• Stripe Payment Processing  
• Financial Application Design  
• Secure Coding Practices  
• Responsive UI Development  
• Software Architecture & Design Patterns  

---

## Getting Started

### Prerequisites

• [.NET 9 SDK](https://dotnet.microsoft.com/download)
• [SQL Server](https://www.microsoft.com/en-us/sql-server) (Express or Developer edition)
• A Gmail account (for email notifications)
• [Stripe account](https://stripe.com) (test keys work fine)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/Edwinuniv/cedarpay.git
   cd cedarpay
   ```

2. **Configure your secrets**
   
   Copy `appsettings.json` and fill in your values:
   ```bash
   cp appsettings.json appsettings.Development.json
   ```

3. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Access the app**
   
   Navigate to `https://localhost:5001` — the database will be seeded automatically with default roles and demo accounts.

### Default Seed Accounts

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@cedarpay.com` | `Admin@123` |
| User | `user@cedarpay.com` | `User@123` |
| Agent | `agent@cedarpay.com` | `Agent@123` |

> ⚠️ **Security Note:** Change these credentials immediately in any non-development environment.

---

## Project Structure

```
CedarPay/
├── ApiControllers/          # REST API controllers
├── Areas/Identity/          # ASP.NET Identity Razor Pages
├── Constants/               # Role constants (Admin, Agent, User)
├── Controllers/             # MVC controllers
│   ├── AccountController    # Profile, reports, Excel export
│   ├── AdminController      # Admin panel (users, KYC, fees)
│   ├── AgentController      # Agent dashboard, cash in/out
│   ├── BotController        # Cedar Bot AI chat
│   ├── ChatController       # Real-time messaging
│   ├── DashboardController  # Main dashboard
│   └── TransferController   # Money transfer logic
├── Data/
│   ├── ApplicationDbContext # EF Core DbContext
│   ├── RoleSeeder           # Seed roles on startup
│   └── UserSeeder           # Seed demo accounts on startup
├── Hubs/
│   └── ChatHub              # SignalR hub for real-time chat
├── Models/                  # Entity models
├── Repositories/            # Repository pattern implementation
├── Services/                # Email, currency exchange, file upload
├── ViewModels/              # View-specific DTOs
├── Views/                   # Razor views
└── wwwroot/                 # Static assets (CSS, JS, images)
```

---

## User Roles

### 🔵 User
Wallets, transfers, beneficiaries, KYC, reviews, chat, bot access, referrals, reports, scheduled transfers.

### 🟢 Agent
All user features, plus: cash operations, commission tracking, store management, agent dashboard, multi-store support.

### 🔴 Admin
Full platform access: user management, KYC review, fee configuration, analytics, exchange rates, announcements, all export features.

---

## API Endpoints

| Endpoint | Description | Auth |
|----------|-------------|------|
| `GET /api/AgentApi` | List all approved agents | Public |
| `GET /api/AgentApi/{id}` | Get agent details | Public |
| `GET /api/AgentApi/city/{city}` | Filter agents by city | Public |
| `GET /api/CurrencyApi` | List active currencies | Public |
| `GET /api/CurrencyApi/rate?from=USD&to=EUR` | Get exchange rate | Public |
| `GET /api/WalletApi` | Get user's wallets | 🔒 Auth |
| `GET /api/WalletApi/balance` | Get total balance | 🔒 Auth |
| `GET /api/TransactionApi` | Get user's transactions | 🔒 Auth |
| `GET /api/TransactionApi/stats` | Get transaction stats | 🔒 Auth |
| `GET /api/NotificationApi` | Get notifications | 🔒 Auth |
| `GET /api/NotificationApi/unread-count` | Get unread count | 🔒 Auth |
| `PUT /api/NotificationApi/{id}/read` | Mark notification read | 🔒 Auth |

---

## Configuration

All settings live in `appsettings.json`. Sensitive values should be stored in environment variables or [.NET User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=MoneyTransfer;..."
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "FromName": "CedarPay",
    "FromEmail": "your-email@gmail.com",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  },
  "Stripe": {
    "PublishableKey": "pk_test_...",
    "SecretKey": "sk_test_..."
  },
  "Authentication": {
    "Google": {
      "ClientId": "...",
      "ClientSecret": "..."
    }
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-2.0-flash-lite"
  },
  "ExchangeRateApi": {
    "BaseUrl": "https://open.er-api.com/v6/latest/"
  }
}
```

> 💡 **Gmail Note:** Requires an [App Password](https://support.google.com/accounts/answer/185833) when 2FA is enabled.

---

## Database Schema

```
User          ──< Wallet        ──< Transaction
              ──< Beneficiary
              ──< Notification
              ──< Agent         ──< Commission
              ──< KYCDocument
              ──< AgentApplication
              ──< Conversation  ──< Message

Currency      ──< Wallet
              ──< Transaction

WalletRequest (pending wallet creation)
FeePolicy     (configurable fee %)
Announcement  (platform broadcasts)
ReferralCode  ──< ReferralUse
ScheduledTransfer
ActivityLog
```

---

## Architecture Overview

```
Client Browser
      │
      ▼
ASP.NET Core MVC
      │
 ┌────┼────┐
 ▼    ▼    ▼
SQL  SignalR Gemini
Server  Hub    API
```

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

---

## License

This project is licensed under the MIT License.

---

## Contact

**Edwin Mouawad**

• Email: [edwinmouawad84@gmail.com](mailto:edwinmouawad84@gmail.com)
• GitHub: [https://github.com/Edwinuniv](https://github.com/Edwinuniv)
• LinkedIn: [https://www.linkedin.com/in/edwin-mouawad-525616362/](https://www.linkedin.com/in/edwin-mouawad-525616362/)

---

<div align="center">

### 🌲 CedarPay

**Send Money • Support Home**

*Built with ❤️ for Lebanon*

</div>
