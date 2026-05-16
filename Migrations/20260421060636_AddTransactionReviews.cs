using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransfer.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TransactionId",
                table: "Reviews",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Transactions_TransactionId",
                table: "Reviews",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Transactions_TransactionId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_TransactionId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Reviews");
        }
    }
}
