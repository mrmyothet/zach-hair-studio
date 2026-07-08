using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LongDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Category", "DisplayOrder", "DurationMinutes", "ImageUrl", "IsActive", "LongDescription", "Name", "Price", "ShortDescription", "Slug" },
                values: new object[,]
                {
                    { 1, "Cuts", 1, 45, null, true, "A tailored cut shaped around your face, texture, and daily styling routine. Includes a consultation and finishing touches so your hair leaves polished and easy to maintain.", "Precision Cut", 35m, "Tailored haircuts designed to complement your face shape and lifestyle perfectly.", "precision-cut" },
                    { 2, "Color", 2, 90, null, true, "Dimensional color and highlight work customized to your skin tone, cut, and maintenance goals. The service uses premium products for glossy color, soft grow-out, and a salon-fresh finish.", "Color & Highlights", 80m, "Vibrant color treatments and natural-looking highlights using premium products.", "color-and-highlights" },
                    { 3, "Styling", 3, 45, null, true, "A smooth professional blowout or styled finish tailored to the occasion, from everyday polish to event-ready volume. Ideal when you want shine, movement, and a finished look without a full cut or color service.", "Blowout & Styling", 55m, "Professional blowouts and styling for any occasion — weddings, events, or everyday glam.", "blowout-and-styling" },
                    { 4, "Treatments", 4, 120, null, true, "A smoothing treatment designed to reduce frizz, increase shine, and make daily styling easier. Best for clients looking for a longer-lasting sleek finish and improved manageability between salon visits.", "Keratin Treatment", 120m, "Smoothing treatments that eliminate frizz and add lasting shine and manageability.", "keratin-treatment" },
                    { 5, "Treatments", 5, 40, null, true, "A restorative scalp-focused service that refreshes, hydrates, and supports a healthier hair environment. Recommended for clients wanting comfort, balance, and a relaxing reset between larger services.", "Scalp Treatment", 65m, "Revitalizing scalp therapies to promote health, hydration, and hair growth.", "scalp-treatment" },
                    { 6, "Styling", 6, 210, null, true, "The full studio transformation package combining a precision cut, color service, blowout, and scalp treatment. Built for clients who want the complete Zach Hair Studio experience in one coordinated visit.", "Full Glam Package", 199m, "Cut + Color + Blowout + Scalp treatment. The complete studio experience in one visit.", "full-glam-package" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_Slug",
                table: "Services",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Services");
        }
    }
}
