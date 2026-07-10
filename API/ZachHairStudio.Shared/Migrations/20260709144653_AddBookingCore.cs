using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stylists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stylists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StylistTimeOff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StylistTimeOff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StylistWorkingHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StylistWorkingHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    SlotStart = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentSlots_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "StylistWorkingHours",
                columns: new[] { "Id", "DayOfWeek", "EndTime", "StartTime", "StylistId" },
                values: new object[,]
                {
                    { 1, 2, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 2, 3, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 3, 4, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 4, 5, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 5, 6, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 6, 2, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 7, 3, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 8, 4, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 9, 5, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 10, 6, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 11, 2, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 12, 3, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 13, 4, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 14, 5, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 15, 6, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 16, 2, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 },
                    { 17, 3, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 },
                    { 18, 4, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 },
                    { 19, 5, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 },
                    { 20, 6, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 }
                });

            migrationBuilder.InsertData(
                table: "Stylists",
                columns: new[] { "Id", "DisplayOrder", "IsActive", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, 1, true, "Mr. Zachary", "mr-zachary" },
                    { 2, 2, true, "Aria Chen", "aria-chen" },
                    { 3, 3, true, "Marcus Lee", "marcus-lee" },
                    { 4, 4, true, "Sofia Reyes", "sofia-reyes" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlots_AppointmentId",
                table: "AppointmentSlots",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentSlots_StylistId_SlotStart",
                table: "AppointmentSlots",
                columns: new[] { "StylistId", "SlotStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stylists_Slug",
                table: "Stylists",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentSlots");

            migrationBuilder.DropTable(
                name: "Stylists");

            migrationBuilder.DropTable(
                name: "StylistTimeOff");

            migrationBuilder.DropTable(
                name: "StylistWorkingHours");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PreferredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Service = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                });
        }
    }
}
