using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Eascess_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanQuotaAndSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxDomains",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyAiQuota",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "DeletedAt", "IsActive", "IsDeleted", "MaxDomains", "MonthlyAiQuota", "MonthlyPrice", "Name" },
                values: new object[,]
                {
                    { 1, null, true, false, 1, 50, 0m, "Ücretsiz" },
                    { 2, null, true, false, 10, 2000, 299m, "Pro" },
                    { 3, null, true, false, 999, 999999, 0m, "Kurumsal" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "MaxDomains",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyAiQuota",
                table: "Plans");
        }
    }
}
