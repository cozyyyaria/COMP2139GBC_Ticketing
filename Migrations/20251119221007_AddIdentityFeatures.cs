using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Rating column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='Purchases' AND column_name='Rating') THEN
                        ALTER TABLE ""Purchases"" ADD COLUMN ""Rating"" integer;
                    END IF;
                END $$;
            ");

            // Add UserId column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='Purchases' AND column_name='UserId') THEN
                        ALTER TABLE ""Purchases"" ADD COLUMN ""UserId"" text;
                    END IF;
                END $$;
            ");

            // Add OrganizerId column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='Events' AND column_name='OrganizerId') THEN
                        ALTER TABLE ""Events"" ADD COLUMN ""OrganizerId"" text;
                    END IF;
                END $$;
            ");

            // Add FullName column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='AspNetUsers' AND column_name='FullName') THEN
                        ALTER TABLE ""AspNetUsers"" ADD COLUMN ""FullName"" text;
                    END IF;
                END $$;
            ");

            // Add ProfileImagePath column if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='AspNetUsers' AND column_name='ProfileImagePath') THEN
                        ALTER TABLE ""AspNetUsers"" ADD COLUMN ""ProfileImagePath"" text;
                    END IF;
                END $$;
            ");

            // Create indexes if they don't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Purchases_UserId') THEN
                        CREATE INDEX ""IX_Purchases_UserId"" ON ""Purchases"" (""UserId"");
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Events_OrganizerId') THEN
                        CREATE INDEX ""IX_Events_OrganizerId"" ON ""Events"" (""OrganizerId"");
                    END IF;
                END $$;
            ");

            // Add foreign keys if they don't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints 
                                   WHERE constraint_name = 'FK_Events_AspNetUsers_OrganizerId') THEN
                        ALTER TABLE ""Events"" 
                        ADD CONSTRAINT ""FK_Events_AspNetUsers_OrganizerId"" 
                        FOREIGN KEY (""OrganizerId"") 
                        REFERENCES ""AspNetUsers"" (""Id"") 
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints 
                                   WHERE constraint_name = 'FK_Purchases_AspNetUsers_UserId') THEN
                        ALTER TABLE ""Purchases"" 
                        ADD CONSTRAINT ""FK_Purchases_AspNetUsers_UserId"" 
                        FOREIGN KEY (""UserId"") 
                        REFERENCES ""AspNetUsers"" (""Id"") 
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_OrganizerId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_AspNetUsers_UserId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_UserId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Events_OrganizerId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "OrganizerId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfileImagePath",
                table: "AspNetUsers");
        }
    }
}
