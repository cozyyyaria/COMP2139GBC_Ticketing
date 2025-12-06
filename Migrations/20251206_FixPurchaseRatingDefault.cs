using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class FixPurchaseRatingDefault2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill any null ratings to 0 so constraint changes succeed
            migrationBuilder.Sql(@"UPDATE ""Purchases"" SET ""Rating"" = 0 WHERE ""Rating"" IS NULL;");

            // Ensure the column defaults to 0 and is non-nullable
            migrationBuilder.Sql(@"ALTER TABLE ""Purchases"" ALTER COLUMN ""Rating"" SET DEFAULT 0;");
            migrationBuilder.Sql(@"ALTER TABLE ""Purchases"" ALTER COLUMN ""Rating"" SET NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop default; keep data intact. Column remains NOT NULL (safer default)
            migrationBuilder.Sql(@"ALTER TABLE ""Purchases"" ALTER COLUMN ""Rating"" DROP DEFAULT;");
        }
    }
}
