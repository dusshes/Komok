using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Komok.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddText2Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Text2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Text2", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Text2",
                columns: new[] { "Id", "fio" },
                values: new object[,]
                {
                    { 1, "fio1" },
                    { 2, "fio2" },
                    { 3, "fio3" }
                });

            migrationBuilder.Sql("""
                SELECT setval(
                    pg_get_serial_sequence('"Text2"', 'Id'),
                    (SELECT MAX("Id") FROM "Text2")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Text2");
        }
    }
}
