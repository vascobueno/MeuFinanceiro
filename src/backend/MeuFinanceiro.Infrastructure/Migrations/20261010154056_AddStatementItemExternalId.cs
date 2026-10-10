using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementItemExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "BankStatementItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementItems_ExternalId",
                table: "BankStatementItems",
                column: "ExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankStatementItems_ExternalId",
                table: "BankStatementItems");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "BankStatementItems");
        }
    }
}
