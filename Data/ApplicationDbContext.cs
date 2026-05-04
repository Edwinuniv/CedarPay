using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Models;

namespace MoneyTransfer.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<FeePolicy> FeePolicies { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<TopUp> TopUps { get; set; }
        public DbSet<Agent> Agents { get; set; }
        public DbSet<Commission> Commissions { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<KYCDocument> KYCDocuments { get; set; }
        public DbSet<AgentApplication> AgentApplications { get; set; }
        public DbSet<WalletRequest> WalletRequests { get; set; }
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<Announcement> Announcements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL");

            modelBuilder.Entity<Account>()
                .HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.Account)
                .WithMany(a => a.Wallets)
                .HasForeignKey(w => w.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.User)
                .WithMany(u => u.Wallets)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.Currency)
                .WithMany(c => c.Wallets)
                .HasForeignKey(w => w.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.SenderWallet)
                .WithMany(w => w.SentTransactions)
                .HasForeignKey(t => t.SenderWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.ReceiverWallet)
                .WithMany(w => w.ReceivedTransactions)
                .HasForeignKey(t => t.ReceiverWalletId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.SenderCurrency)
                .WithMany()
                .HasForeignKey(t => t.SenderCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.ReceiverCurrency)
                .WithMany()
                .HasForeignKey(t => t.ReceiverCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Commission)
                .WithOne(c => c.Transaction)
                .HasForeignKey<Commission>(c => c.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TopUp>()
                .HasOne(t => t.Wallet)
                .WithMany(w => w.TopUps)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TopUp>()
                .HasOne(t => t.Currency)
                .WithMany(c => c.TopUps)
                .HasForeignKey(t => t.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Agent>()
                .HasOne(a => a.User)
                .WithMany(u => u.Agents)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Commission>()
                .HasOne(c => c.Agent)
                .WithMany(a => a.Commissions)
                .HasForeignKey(c => c.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Agent)
                .WithMany(a => a.Reviews)
                .HasForeignKey(r => r.AgentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<KYCDocument>()
                .HasOne(k => k.User)
                .WithOne(u => u.KYCDocument)
                .HasForeignKey<KYCDocument>(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupportTicket>()
                .HasOne(s => s.User)
                .WithMany(u => u.SupportTickets)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Beneficiary>()
                .HasOne(b => b.User)
                .WithMany(u => u.Beneficiaries)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WalletRequest>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WalletRequest>()
                .HasOne(w => w.Currency)
                .WithMany()
                .HasForeignKey(w => w.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConversationParticipant>()
                .HasOne(cp => cp.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => cp.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConversationParticipant>()
                .HasOne(cp => cp.User)
                .WithMany()
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Wallet>()
                .Property(w => w.Balance)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.ConvertedAmount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.FeeAmount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.ExchangeRateUsed)
                .HasPrecision(18, 6);

            modelBuilder.Entity<TopUp>()
                .Property(t => t.Amount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Commission>()
                .Property(c => c.Amount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Currency>()
                .Property(c => c.ExchangeRateToUSD)
                .HasPrecision(18, 6);

            modelBuilder.Entity<FeePolicy>()
                .Property(f => f.FeePercentage)
                .HasPrecision(5, 4);

            modelBuilder.Entity<FeePolicy>()
                .Property(f => f.FixedFee)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Agent>()
                .Property(a => a.CommissionRate)
                .HasPrecision(5, 4);

            modelBuilder.Entity<Commission>()
                .Property(c => c.Percentage)
                .HasPrecision(5, 4);

            modelBuilder.Entity<AgentApplication>()
                .HasOne(a => a.User)
                .WithMany(u => u.AgentApplications)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MessageReaction>()
                .HasIndex(r => new { r.MessageId, r.UserId, r.Emoji })
                .IsUnique();

            modelBuilder.Entity<Currency>().HasData(
                new Currency { Id = 1, Code = "USD", Name = "US Dollar", Symbol = "$", FlagUrl = "/images/flags/usd.png", ExchangeRateToUSD = 1.000000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 2, Code = "EUR", Name = "Euro", Symbol = "€", FlagUrl = "/images/flags/eur.png", ExchangeRateToUSD = 1.080000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 3, Code = "LBP", Name = "Lebanese Pound", Symbol = "ل.ل", FlagUrl = "/images/flags/lbp.png", ExchangeRateToUSD = 0.000011m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 4, Code = "AED", Name = "UAE Dirham", Symbol = "د.إ", FlagUrl = "/images/flags/aed.png", ExchangeRateToUSD = 0.272000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 5, Code = "GBP", Name = "British Pound", Symbol = "£", FlagUrl = "/images/flags/gbp.png", ExchangeRateToUSD = 1.270000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 6, Code = "SAR", Name = "Saudi Riyal", Symbol = "﷼", FlagUrl = "/images/flags/sar.png", ExchangeRateToUSD = 0.266000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 7, Code = "TRY", Name = "Turkish Lira", Symbol = "₺", FlagUrl = "/images/flags/try.png", ExchangeRateToUSD = 0.031000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 8, Code = "EGP", Name = "Egyptian Pound", Symbol = "E£", FlagUrl = "/images/flags/egp.png", ExchangeRateToUSD = 0.021000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 9, Code = "JOD", Name = "Jordanian Dinar", Symbol = "JD", FlagUrl = "/images/flags/jod.png", ExchangeRateToUSD = 1.410000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) },
                new Currency { Id = 10, Code = "KWD", Name = "Kuwaiti Dinar", Symbol = "KD", FlagUrl = "/images/flags/kwd.png", ExchangeRateToUSD = 3.240000m, IsActive = true, LastUpdated = new DateTime(2026, 1, 1) }
            );

            modelBuilder.Entity<FeePolicy>().HasData(
                new FeePolicy { Id = 1, Name = "Standard", FeePercentage = 0.02m, FixedFee = 0.50m, FreeTransactionThreshold = 10, IsActive = true, CreatedAt = new DateTime(2026, 1, 1) }
            );
        }
    }
}
