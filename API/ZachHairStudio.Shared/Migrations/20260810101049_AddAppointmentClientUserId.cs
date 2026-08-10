using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentClientUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientUserId",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClientUserId",
                table: "Appointments",
                column: "ClientUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_AspNetUsers_ClientUserId",
                table: "Appointments",
                column: "ClientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_AspNetUsers_ClientUserId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClientUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClientUserId",
                table: "Appointments");
        }
    }
}
