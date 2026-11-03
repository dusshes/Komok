using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Komok.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialTextTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    CREATE TABLE IF NOT EXISTS "Text" (
                        "Txt" character varying(500) NOT NULL
                    );

                    IF NOT EXISTS (SELECT 1 FROM "Text") THEN
                        INSERT INTO "Text" ("Txt") VALUES ('Komok');
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This initial migration also adopts databases where Text predates EF migrations.
            // Keep the table and its data if the migration history is rolled back.
        }
    }
}
