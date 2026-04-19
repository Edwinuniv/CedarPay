using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IBeneficiaryRepository: IGenericRepository<Beneficiary>
    {
        Task<IEnumerable<Beneficiary>> GetByUserIdAsync(string userId);
        Task<Beneficiary?> GetByNicknameAsync(string userId, string nickname);
        Task<bool> ExistsByWalletSerialAsync(string userId, string walletSerial);
    }
}
