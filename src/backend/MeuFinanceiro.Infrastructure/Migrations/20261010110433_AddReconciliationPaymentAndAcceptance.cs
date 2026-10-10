using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationPaymentAndAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Reconciliations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CardPaymentId",
                table: "Reconciliations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_CardPaymentId",
                table: "Reconciliations",
                column: "CardPaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reconciliations_CardPayments_CardPaymentId",
                table: "Reconciliations",
                column: "CardPaymentId",
                principalTable: "CardPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Até aqui o aceite só ficava no Status Reconciled do lançamento. As conciliações
            // aceitas antes desta migração ganham a data da última alteração como aceite.
            migrationBuilder.Sql("""
                UPDATE "Reconciliations" r
                SET "AcceptedAt" = COALESCE(r."UpdatedAt", r."CreatedAt")
                FROM "FinancialTransactions" t
                WHERE t."Id" = r."FinancialTransactionId"
                  AND t."Status" = 3
                  AND r."Status" IN (3, 4);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reconciliations_CardPayments_CardPaymentId",
                table: "Reconciliations");

            migrationBuilder.DropIndex(
                name: "IX_Reconciliations_CardPaymentId",
                table: "Reconciliations");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Reconciliations");

            migrationBuilder.DropColumn(
                name: "CardPaymentId",
                table: "Reconciliations");
        }
    }
}
