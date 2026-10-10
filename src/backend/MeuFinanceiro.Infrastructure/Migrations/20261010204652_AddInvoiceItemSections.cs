using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardLastFour",
                table: "CardInvoiceItems",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "CardInvoiceItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExchangeRateEstimated",
                table: "CardInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ForeignAmount",
                table: "CardInvoiceItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForeignCurrency",
                table: "CardInvoiceItems",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                table: "CardInvoiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentTotal",
                table: "CardInvoiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssuedCardId",
                table: "CardInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CardInvoiceItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Expense");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "CardInvoiceItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCurrency",
                table: "CardInvoiceItems",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedItemId",
                table: "CardInvoiceItems",
                type: "uuid",
                nullable: true);

            // Itens importados antes: tipo e número/total da parcela pelas mesmas regras de
            // InvoiceTextReader.Classify, na mesma ordem (sinal, IOF, tarifa, parcela).
            migrationBuilder.Sql("""
                UPDATE "CardInvoiceItems" SET "Kind" = CASE
                    WHEN "Amount" < 0 AND (upper("Description") LIKE '%PAGAMENTO%' OR upper("Description") LIKE '%PAGTO%') THEN 'Payment'
                    WHEN "Amount" < 0 THEN 'Credit'
                    WHEN upper("Description") LIKE 'IOF%' THEN 'Tax'
                    WHEN upper("Description") ~ '\m(ANUIDADE|TARIFA|SEGURO|JUROS|MULTA|ENCARGO|ENCARGOS)\M' THEN 'Fee'
                    WHEN "InstallmentLabel" ~ '^\s*(parcela\s+)?\d{1,3}\s*[/-]\s*\d{1,3}\s*$' THEN 'Installment'
                    ELSE 'Expense'
                END;
                UPDATE "CardInvoiceItems"
                SET "InstallmentNumber" = (regexp_match("InstallmentLabel", '(\d{1,3})\s*[/-]\s*(\d{1,3})'))[1]::int,
                    "InstallmentTotal" = (regexp_match("InstallmentLabel", '(\d{1,3})\s*[/-]\s*(\d{1,3})'))[2]::int
                WHERE "InstallmentLabel" ~ '\d{1,3}\s*[/-]\s*\d{1,3}';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CardInvoiceItems_IssuedCardId",
                table: "CardInvoiceItems",
                column: "IssuedCardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardInvoiceItems_RelatedItemId",
                table: "CardInvoiceItems",
                column: "RelatedItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_CardInvoiceItems_CardInvoiceItems_RelatedItemId",
                table: "CardInvoiceItems",
                column: "RelatedItemId",
                principalTable: "CardInvoiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CardInvoiceItems_IssuedCards_IssuedCardId",
                table: "CardInvoiceItems",
                column: "IssuedCardId",
                principalTable: "IssuedCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardInvoiceItems_CardInvoiceItems_RelatedItemId",
                table: "CardInvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CardInvoiceItems_IssuedCards_IssuedCardId",
                table: "CardInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_CardInvoiceItems_IssuedCardId",
                table: "CardInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_CardInvoiceItems_RelatedItemId",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "CardLastFour",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ExchangeRateEstimated",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ForeignAmount",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ForeignCurrency",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "InstallmentTotal",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "IssuedCardId",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "OriginalCurrency",
                table: "CardInvoiceItems");

            migrationBuilder.DropColumn(
                name: "RelatedItemId",
                table: "CardInvoiceItems");
        }
    }
}
