using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZachHairStudio.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LongDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRecommendedProduct",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRecommendedProduct", x => new { x.ServiceId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ServiceRecommendedProduct_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceRecommendedProduct_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "ImageUrl", "IsActive", "LongDescription", "Name", "Price", "ShortDescription", "Slug", "Stock" },
                values: new object[,]
                {
                    { 1, "Hair Care", null, true, "A lightweight leave-in serum formulated to extend the smoothing effects of a keratin treatment between salon visits. Applies to damp or dry hair to reduce frizz and add shine without weighing hair down.", "Leave-In Repair Serum", 24.00m, "A lightweight leave-in serum that locks in smoothness after a keratin service.", "leave-in-repair-serum", 40 },
                    { 2, "Hair Care", null, true, "A sulfate-free, color-safe shampoo designed to preserve tone and shine after a color or highlight service. Gently cleanses without stripping the color molecules that give fresh color its vibrancy.", "Color-Safe Shampoo", 18.00m, "A sulfate-free shampoo that protects vibrant color and highlights from fading.", "color-safe-shampoo", 60 },
                    { 3, "Hair Care", null, true, "A nourishing, color-safe conditioner formulated to pair with the color-safe shampoo. Softens and detangles while helping lock in color vibrancy between coloring appointments.", "Color-Safe Conditioner", 19.00m, "A nourishing conditioner that pairs with our color-safe shampoo to extend color life.", "color-safe-conditioner", 55 },
                    { 4, "Styling", null, true, "A flexible-hold styling cream that adds definition and texture without stiffness, perfect for extending a fresh blowout or building volume for an event-ready look.", "Texturizing Styling Cream", 22.00m, "A flexible-hold cream for defined texture and movement after a blowout.", "texturizing-styling-cream", 0 },
                    { 5, "Styling", null, true, "A lightweight, non-greasy spray applied before blow-drying or hot tools to shield hair from heat damage, helping styled looks last longer between salon visits.", "Heat Protectant Spray", 16.00m, "A lightweight spray that shields hair from heat styling damage.", "heat-protectant-spray", 50 },
                    { 6, "Treatments", null, true, "A soothing, lightweight scalp oil blended to extend the hydrating benefits of an in-salon scalp treatment. Massage into the scalp between visits to support comfort and a healthier hair environment.", "Revitalizing Scalp Oil", 28.00m, "A soothing scalp oil that extends the benefits of an in-salon scalp treatment.", "revitalizing-scalp-oil", 30 },
                    { 7, "Styling", null, false, "A retired matte styling wax no longer sold in the studio. Present only so the inactive-product 404 and enumeration-safety paths have a real seeded row to exercise.", "Discontinued Styling Wax", 15.00m, "A retired matte styling wax, kept only to exercise the inactive-product path.", "discontinued-styling-wax", 0 }
                });

            migrationBuilder.InsertData(
                table: "ServiceRecommendedProduct",
                columns: new[] { "ProductId", "ServiceId" },
                values: new object[,]
                {
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 3 },
                    { 5, 3 },
                    { 1, 4 },
                    { 6, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRecommendedProduct_ProductId",
                table: "ServiceRecommendedProduct",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRecommendedProduct");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
