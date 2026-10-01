using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCardPaymentCycleKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CycleClosingDate",
                table: "CardPayments",
                newName: "CycleReference");

            migrationBuilder.RenameIndex(
                name: "IX_CardPayments_CreditCardId_CycleClosingDate",
                table: "CardPayments",
                newName: "IX_CardPayments_CreditCardId_CycleReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CycleReference",
                table: "CardPayments",
                newName: "CycleClosingDate");

            migrationBuilder.RenameIndex(
                name: "IX_CardPayments_CreditCardId_CycleReference",
                table: "CardPayments",
                newName: "IX_CardPayments_CreditCardId_CycleClosingDate");
        }
    }
}
