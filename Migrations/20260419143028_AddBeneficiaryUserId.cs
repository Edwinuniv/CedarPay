using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransfer.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiaryUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiverUserId",
                table: "Beneficiaries",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_ReceiverUserId",
                table: "Beneficiaries",
                column: "ReceiverUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Beneficiaries_AspNetUsers_ReceiverUserId",
                table: "Beneficiaries",
                column: "ReceiverUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Beneficiaries_AspNetUsers_ReceiverUserId",
                table: "Beneficiaries");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_ReceiverUserId",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "ReceiverUserId",
                table: "Beneficiaries");
        }
    }
}
