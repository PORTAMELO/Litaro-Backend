using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddWebContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebContentConfiguration",
                columns: table => new
                {
                    WebContentConfigurationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ContentKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MinItems = table.Column<int>(type: "int", nullable: false),
                    MaxItems = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebContentConfiguration", x => x.WebContentConfigurationId);
                    table.UniqueConstraint("AK_WebContentConfiguration_PageName_SectionName_ContentKey", x => new { x.PageName, x.SectionName, x.ContentKey });
                });

            migrationBuilder.CreateTable(
                name: "WebContent",
                columns: table => new
                {
                    WebContentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ContentKey = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebContent", x => x.WebContentId);
                    table.ForeignKey(
                        name: "FK_WebContent_WebContentConfiguration_PageName_SectionName_ContentKey",
                        columns: x => new { x.PageName, x.SectionName, x.ContentKey },
                        principalTable: "WebContentConfiguration",
                        principalColumns: new[] { "PageName", "SectionName", "ContentKey" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebContent_PageName_SectionName_ContentKey",
                table: "WebContent",
                columns: new[] { "PageName", "SectionName", "ContentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_WebContent_PageName_SectionName_DisplayOrder",
                table: "WebContent",
                columns: new[] { "PageName", "SectionName", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WebContentConfiguration_PageName_SectionName_ContentKey",
                table: "WebContentConfiguration",
                columns: new[] { "PageName", "SectionName", "ContentKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebContent");

            migrationBuilder.DropTable(
                name: "WebContentConfiguration");
        }
    }
}
