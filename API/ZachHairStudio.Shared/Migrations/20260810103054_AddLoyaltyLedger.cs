using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientUserId = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyLedgers_AspNetUsers_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyLedgers_AppointmentId",
                table: "LoyaltyLedgers",
                column: "AppointmentId",
                unique: true,
                filter: "[AppointmentId] IS NOT NULL AND [Reason] = N'Earn'");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyLedgers_ClientUserId",
                table: "LoyaltyLedgers",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyLedgers_OrderId",
                table: "LoyaltyLedgers",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyLedgers");
        }
    }
}
