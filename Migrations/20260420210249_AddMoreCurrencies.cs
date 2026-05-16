using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MoneyTransfer.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "ExchangeRateToUSD", "FlagUrl", "IsActive", "LastUpdated", "Name", "Symbol" },
                values: new object[,]
                {
                    { 5, "GBP", 1.270000m, "/images/flags/gbp.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "British Pound", "£" },
                    { 6, "SAR", 0.266000m, "/images/flags/sar.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Saudi Riyal", "﷼" },
                    { 7, "TRY", 0.031000m, "/images/flags/try.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Turkish Lira", "₺" },
                    { 8, "EGP", 0.021000m, "/images/flags/egp.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Egyptian Pound", "E£" },
                    { 9, "JOD", 1.410000m, "/images/flags/jod.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Jordanian Dinar", "JD" },
                    { 10, "KWD", 3.240000m, "/images/flags/kwd.png", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Kuwaiti Dinar", "KD" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}
