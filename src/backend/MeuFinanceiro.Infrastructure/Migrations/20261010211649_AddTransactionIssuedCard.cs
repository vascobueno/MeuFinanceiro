using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIssuedCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IssuedCardId",
                table: "FinancialTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_IssuedCardId",
                table: "FinancialTransactions",
                column: "IssuedCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_IssuedCards_IssuedCardId",
                table: "FinancialTransactions",
                column: "IssuedCardId",
                principalTable: "IssuedCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_IssuedCards_IssuedCardId",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_IssuedCardId",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "IssuedCardId",
                table: "FinancialTransactions");
        }
    }
}
