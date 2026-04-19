using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransfer.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultWalletSerial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentApplications_AspNetUsers_UserId1",
                table: "AgentApplications");

            migrationBuilder.DropIndex(
                name: "IX_AgentApplications_UserId1",
                table: "AgentApplications");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "AgentApplications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "AgentApplications",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentApplications_UserId1",
                table: "AgentApplications",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentApplications_AspNetUsers_UserId1",
                table: "AgentApplications",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
