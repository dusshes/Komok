using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Komok.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerMessengerToBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerMessenger",
                table: "Products",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerMessengerContact",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerMessenger",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BuyerMessengerContact",
                table: "Products");
        }
    }
}
