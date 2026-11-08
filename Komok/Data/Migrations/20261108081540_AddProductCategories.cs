using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Komok.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "NormalizedName", "IsActive" },
                values: new object[,]
                {
                    { 1, "Одежда", "ОДЕЖДА", true },
                    { 2, "Обувь", "ОБУВЬ", true },
                    { 3, "Сумки и аксессуары", "СУМКИ И АКСЕССУАРЫ", true },
                    { 4, "Украшения", "УКРАШЕНИЯ", true },
                    { 5, "Мебель", "МЕБЕЛЬ", true },
                    { 6, "Дом и интерьер", "ДОМ И ИНТЕРЬЕР", true },
                    { 7, "Посуда", "ПОСУДА", true },
                    { 8, "Книги", "КНИГИ", true },
                    { 9, "Электроника", "ЭЛЕКТРОНИКА", true },
                    { 10, "Бытовая техника", "БЫТОВАЯ ТЕХНИКА", true },
                    { 11, "Детские товары", "ДЕТСКИЕ ТОВАРЫ", true },
                    { 12, "Спорт и отдых", "СПОРТ И ОТДЫХ", true },
                    { 13, "Авто/мото", "АВТО/МОТО", true },
                    { 14, "Велосипеды и самокаты", "ВЕЛОСИПЕДЫ И САМОКАТЫ", true },
                    { 15, "Инструменты и оборудование", "ИНСТРУМЕНТЫ И ОБОРУДОВАНИЕ", true },
                    { 16, "Музыкальные инструменты", "МУЗЫКАЛЬНЫЕ ИНСТРУМЕНТЫ", true },
                    { 17, "Коллекционирование", "КОЛЛЕКЦИОНИРОВАНИЕ", true },
                    { 18, "Другое", "ДРУГОЕ", true }
                });
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"Categories\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Categories\"));");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Products\" SET \"CategoryId\" = 18 WHERE \"CategoryId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Products\" SET \"PublishedAt\" = \"CreatedAt\" WHERE \"ListingStatus\" = 5 AND \"PublishedAt\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Products");
        }
    }
}
