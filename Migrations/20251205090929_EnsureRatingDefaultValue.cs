using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class EnsureRatingDefaultValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure Rating column has default value of 0 in the database
            migrationBuilder.Sql(@"
                -- Update any existing NULL values to 0 first
                UPDATE ""Purchases"" 
                SET ""Rating"" = 0 
                WHERE ""Rating"" IS NULL;
                
                -- Set default value
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" SET DEFAULT 0;
                
                -- Ensure column is NOT NULL
                ALTER TABLE ""Purchases"" 
                ALTER COLUMN ""Rating"" SET NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "Purchases",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false,
                oldDefaultValue: 0);
        }
    }
}
