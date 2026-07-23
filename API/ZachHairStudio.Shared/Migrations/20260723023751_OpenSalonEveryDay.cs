using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class OpenSalonEveryDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StylistWorkingHours",
                columns: new[] { "Id", "DayOfWeek", "EndTime", "StartTime", "StylistId" },
                values: new object[,]
                {
                    { 21, 0, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 22, 1, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 1 },
                    { 23, 0, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 24, 1, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 2 },
                    { 25, 0, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 26, 1, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 3 },
                    { 27, 0, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 },
                    { 28, 1, new TimeOnly(18, 0, 0), new TimeOnly(9, 0, 0), 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "StylistWorkingHours",
                keyColumn: "Id",
                keyValue: 28);
        }
    }
}
