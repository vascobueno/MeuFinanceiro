using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BalanceValidated",
                table: "CardInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "CardInvoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousBalance",
                table: "CardInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "BalanceValidated",
                table: "BankStatements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "BankStatements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Extratos e faturas já importados: sem isto, todos apareceriam como "não confere",
            // quando na verdade a conferência nunca foi gravada. Recalcula com a mesma regra da
            // importação (fatura antiga não tinha saldo anterior, então ele é zero).
            migrationBuilder.Sql(@"
                UPDATE ""BankStatements"" s
                   SET ""BalanceValidated"" = (s.""OpeningBalance"" + COALESCE((
                        SELECT SUM(i.""Amount"") FROM ""BankStatementItems"" i WHERE i.""BankStatementId"" = s.""Id""), 0)
                        = s.""ClosingBalance"");");
            migrationBuilder.Sql(@"
                UPDATE ""CardInvoices"" c
                   SET ""BalanceValidated"" = (COALESCE((
                        SELECT SUM(i.""Amount"") FROM ""CardInvoiceItems"" i WHERE i.""CardInvoiceId"" = c.""Id""), 0)
                        = c.""TotalAmount"");");

            migrationBuilder.CreateIndex(
                name: "IX_CardInvoices_GrupoId_ContentHash",
                table: "CardInvoices",
                columns: new[] { "GrupoId", "ContentHash" },
                unique: true,
                filter: "\"ContentHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_GrupoId_ContentHash",
                table: "BankStatements",
                columns: new[] { "GrupoId", "ContentHash" },
                unique: true,
                filter: "\"ContentHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardInvoices_GrupoId_ContentHash",
                table: "CardInvoices");

            migrationBuilder.DropIndex(
                name: "IX_BankStatements_GrupoId_ContentHash",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "BalanceValidated",
                table: "CardInvoices");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "CardInvoices");

            migrationBuilder.DropColumn(
                name: "PreviousBalance",
                table: "CardInvoices");

            migrationBuilder.DropColumn(
                name: "BalanceValidated",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "BankStatements");
        }
    }
}
