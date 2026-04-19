using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransfer.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RecieverPhoneNumber",
                table: "Transactions",
                newName: "ReceiverPhoneNumber");

            migrationBuilder.RenameColumn(
                name: "RecieverName",
                table: "Transactions",
                newName: "ReceiverName");

            migrationBuilder.RenameColumn(
                name: "PaymentRefrence",
                table: "TopUps",
                newName: "PaymentReference");

            migrationBuilder.AddColumn<int>(
                name: "TransactionCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionCount",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "ReceiverPhoneNumber",
                table: "Transactions",
                newName: "RecieverPhoneNumber");

            migrationBuilder.RenameColumn(
                name: "ReceiverName",
                table: "Transactions",
                newName: "RecieverName");

            migrationBuilder.RenameColumn(
                name: "PaymentReference",
                table: "TopUps",
                newName: "PaymentRefrence");
        }
    }
}
