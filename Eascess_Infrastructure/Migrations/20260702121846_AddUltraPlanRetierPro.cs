using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eascess_Infrastructure.Migrations
{
    /// <summary>
    /// Fiyatlandırma yeniden yapılandırması (2026-07-02):
    /// - Pro yeniden kademelendi: 3 domain / aylık 500 AI taraması (fiyat 299₺ değişmedi).
    ///   E-posta bildirimleri ve öncelikli destek Ultra'ya taşındı (pazarlama katmanında;
    ///   bu özelliklerin plan bazlı zorunlu kılınması ödeme entegrasyonuyla gelecek).
    /// - Yeni Ultra planı (Id=4): eski Pro kapsamı — 10 domain / 2.000 AI, aylık 500₺.
    /// Fiyatlar KDV hariçtir. Yıllık planlar (Pro 3.000₺ / Ultra 5.000₺) yalnızca
    /// pazarlama sayfasında sunulur; ödeme altyapısı olmadığından ayrı plan satırı yoktur.
    /// </summary>
    public partial class AddUltraPlanRetierPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MaxDomains", "MonthlyAiQuota" },
                values: new object[] { 3, 500 });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "DeletedAt", "IsActive", "IsDeleted", "MaxDomains", "MonthlyAiQuota", "MonthlyPrice", "Name" },
                values: new object[] { 4, null, true, false, 10, 2000, 500m, "Ultra" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MaxDomains", "MonthlyAiQuota" },
                values: new object[] { 10, 2000 });
        }
    }
}
