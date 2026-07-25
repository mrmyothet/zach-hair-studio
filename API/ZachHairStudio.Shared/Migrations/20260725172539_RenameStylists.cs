using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RenameStylists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Zin Min", "zin-min" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "May Yoon", "may-yoon" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Thiri Cho", "thiri-cho" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Sai Min Htet", "sai-min-htet" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Mr. Zachary", "mr-zachary" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Aria Chen", "aria-chen" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Marcus Lee", "marcus-lee" });

            migrationBuilder.UpdateData(
                table: "Stylists",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Sofia Reyes", "sofia-reyes" });
        }
    }
}
