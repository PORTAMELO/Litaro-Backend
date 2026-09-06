using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacteristicTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Characteristic",
                schema: "public",
                columns: table => new
                {
                    CharacteristicId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characteristic", x => x.CharacteristicId);
                });

            migrationBuilder.CreateTable(
                name: "CharacteristicDetail",
                schema: "public",
                columns: table => new
                {
                    CharacteristicDetailId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CharacteristicId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Valor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacteristicDetail", x => x.CharacteristicDetailId);
                    table.ForeignKey(
                        name: "FK_CharacteristicDetail_Characteristic_CharacteristicId",
                        column: x => x.CharacteristicId,
                        principalSchema: "public",
                        principalTable: "Characteristic",
                        principalColumn: "CharacteristicId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnConfigurations_CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations",
                column: "CharacteristicId");

            migrationBuilder.CreateIndex(
                name: "IX_Characteristic_Name",
                schema: "public",
                table: "Characteristic",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacteristicDetail_CharacteristicId",
                schema: "public",
                table: "CharacteristicDetail",
                column: "CharacteristicId");

            migrationBuilder.AddForeignKey(
                name: "FK_ColumnConfigurations_Characteristic_CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations",
                column: "CharacteristicId",
                principalSchema: "public",
                principalTable: "Characteristic",
                principalColumn: "CharacteristicId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ColumnConfigurations_Characteristic_CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations");

            migrationBuilder.DropTable(
                name: "CharacteristicDetail",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Characteristic",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_ColumnConfigurations_CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations");

            migrationBuilder.DropColumn(
                name: "CharacteristicId",
                schema: "public",
                table: "ColumnConfigurations");
        }
    }
}
