using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedCardsAndCardBrand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bandeira era texto livre; vira o nome de CardBrand. Antes do AlterColumn: texto mais
            // longo que 20 faria a mudança de tipo falhar. Em branco vira nulo ("não informada");
            // qualquer outro valor que não seja uma bandeira conhecida vira "Outra".
            migrationBuilder.Sql("""
                UPDATE "CreditCards" SET "Brand" = CASE lower(btrim("Brand"))
                    WHEN '' THEN NULL
                    WHEN 'visa' THEN 'Visa'
                    WHEN 'master' THEN 'Mastercard'
                    WHEN 'mastercard' THEN 'Mastercard'
                    WHEN 'master card' THEN 'Mastercard'
                    WHEN 'elo' THEN 'Elo'
                    WHEN 'amex' THEN 'AmericanExpress'
                    WHEN 'american express' THEN 'AmericanExpress'
                    WHEN 'americanexpress' THEN 'AmericanExpress'
                    WHEN 'hipercard' THEN 'Hipercard'
                    WHEN 'hiper' THEN 'Hipercard'
                    WHEN 'diners' THEN 'DinersClub'
                    WHEN 'diners club' THEN 'DinersClub'
                    WHEN 'dinersclub' THEN 'DinersClub'
                    WHEN 'jcb' THEN 'Jcb'
                    WHEN 'discover' THEN 'Discover'
                    WHEN 'unionpay' THEN 'UnionPay'
                    WHEN 'union pay' THEN 'UnionPay'
                    WHEN 'cabal' THEN 'Cabal'
                    ELSE 'Outra'
                END
                WHERE "Brand" IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "CreditCards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "IssuedCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FirstDigits = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    LastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ExpiryMonth = table.Column<int>(type: "integer", nullable: true),
                    ExpiryYear = table.Column<int>(type: "integer", nullable: true),
                    HolderName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssuedCards_CreditCards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssuedCards_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuedCards_IssuedCards_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "IssuedCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCards_CreditCardId",
                table: "IssuedCards",
                column: "CreditCardId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCards_GrupoId",
                table: "IssuedCards",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCards_GrupoId_LastFour",
                table: "IssuedCards",
                columns: new[] { "GrupoId", "LastFour" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCards_ReplacedById",
                table: "IssuedCards",
                column: "ReplacedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssuedCards");

            migrationBuilder.AlterColumn<string>(
                name: "Brand",
                table: "CreditCards",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
