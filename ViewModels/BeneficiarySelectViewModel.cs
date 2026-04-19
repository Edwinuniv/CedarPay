namespace MoneyTransfer.ViewModels
{
    public class BeneficiarySelectViewModel
    {
        public int Id { get; set; }
        public string Nickname { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverWalletSerial { get; set; }
        public string? ReceiverPhoneNumber { get; set; }
        public string? Type { get; set; }
    }
}
