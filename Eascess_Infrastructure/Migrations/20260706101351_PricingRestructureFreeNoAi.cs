using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eascess_Infrastructure.Migrations
{
    /// <summary>
    /// Fiyatlandırma yeniden yapılandırması (2026-07-06):
    /// - Ücretsiz plan: AI görsel taraması kaldırıldı (MonthlyAiQuota 50 → 0).
    ///   Raporlama da ücretsiz plandan kaldırıldı (pazarlama katmanında; plan bazlı
    ///   zorunlu kılınması ödeme entegrasyonuyla gelecek).
    /// - Pro: aylık 299₺ → 600₺ (kapsam değişmedi: 3 domain / 500 AI).
    /// - Ultra: aylık 500₺ → 1.000₺ (kapsam değişmedi: 10 domain / 2.000 AI).
    /// Yıllık planlar 10 ay ücretine göre güncellendi: Pro 6.000₺ / Ultra 10.000₺
    /// (yalnızca pazarlama sayfasında; ayrı plan satırı yok). Fiyatlar KDV hariçtir.
    ///
    /// Not: Ultra (Id=4) daha önce AddUltraPlanRetierPro migration'ında elle eklendiği
    /// için model snapshot'ında yoktu; bu migration ile HasData'ya alındı. Scaffold'un
    /// ürettiği InsertData, satır DB'de zaten var olduğundan UpdateData'ya çevrildi.
    /// </summary>
    public partial class PricingRestructureFreeNoAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "MonthlyAiQuota",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MaxDomains", "MonthlyAiQuota", "MonthlyPrice" },
                values: new object[] { 3, 500, 600m });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "MonthlyPrice",
                value: 1000m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "MonthlyPrice",
                value: 500m);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "MonthlyAiQuota",
                value: 50);

            // AddUltraPlanRetierPro sonrası fiili durum: 3 domain / 500 AI / 299₺
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MaxDomains", "MonthlyAiQuota", "MonthlyPrice" },
                values: new object[] { 3, 500, 299m });
        }
    }
}
