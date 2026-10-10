using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBankDetailsAndOverdraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountDigit",
                table: "Accounts",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Accounts",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Agency",
                table: "Accounts",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgencyDigit",
                table: "Accounts",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "Accounts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverdraftLimit",
                table: "Accounts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_GrupoId_BankCode_Agency_AccountNumber_AccountDigit",
                table: "Accounts",
                columns: new[] { "GrupoId", "BankCode", "Agency", "AccountNumber", "AccountDigit" },
                unique: true,
                filter: "\"AccountNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_GrupoId_BankCode_Agency_AccountNumber_AccountDigit",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountDigit",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Agency",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AgencyDigit",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OverdraftLimit",
                table: "Accounts");
        }
    }
}
