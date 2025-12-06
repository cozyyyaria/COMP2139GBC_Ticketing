using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class FixPurchaseRatingDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Update any existing NULL values to 0
            migrationBuilder.Sql(@"
                UPDATE ""Purchases"" 
                SET ""Rating"" = 0 
                WHERE ""Rating"" IS NULL;
            ");
            
            // Step 2: Set default value in database
            migrationBuilder.Sql(@"
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" SET DEFAULT 0;
            ");
            
            // Step 3: Ensure column is NOT NULL (if not already)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" SET NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: Remove NOT NULL constraint
            migrationBuilder.Sql(@"
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" DROP NOT NULL;
            ");
            
            // Reverse: Remove default value
            migrationBuilder.Sql(@"
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" DROP DEFAULT;
            ");
        }
    }
}
