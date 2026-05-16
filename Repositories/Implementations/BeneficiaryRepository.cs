using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class BeneficiaryRepository : GenericRepository<Beneficiary>, IBeneficiaryRepository
    {
        public BeneficiaryRepository(ApplicationDbContext context) : base(context)
        { 
            
        }

        public async Task<IEnumerable<Beneficiary>> GetByUserIdAsync(string userId)
        {
            return await _context.Beneficiaries
                .Include(b => b.ReceiverUser) 
                .Where(b => b.UserId == userId)
                .OrderBy(b => b.Nickname)
                .ToListAsync();
        }

        public async Task<Beneficiary?> GetByNicknameAsync(string userId, string nickname)
        {
            return await _context.Beneficiaries.Include(b => b.ReceiverUser).FirstOrDefaultAsync(b => b.UserId == userId && b.Nickname == nickname);
        }

        public async Task<bool> ExistsByWalletSerialAsync(string userId, string walletSerial)
        {
            return await _context.Beneficiaries.AnyAsync(b => b.UserId == userId && b.ReceiverWalletSerial == walletSerial);
        }
    }
}