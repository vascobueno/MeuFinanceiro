using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionForeignPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "FinancialTransactions",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForeignAmount",
                table: "FinancialTransactions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForeignCurrency",
                table: "FinancialTransactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "FinancialTransactions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCurrency",
                table: "FinancialTransactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ForeignAmount",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "ForeignCurrency",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalCurrency",
                table: "FinancialTransactions");
        }
    }
}
