using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPayments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardPayments_CreditCards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardPayments_FinancialTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "FinancialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CardPayments_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_AccountId",
                table: "CardPayments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_CreditCardId_CycleClosingDate",
                table: "CardPayments",
                columns: new[] { "CreditCardId", "CycleClosingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_GrupoId",
                table: "CardPayments",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPayments_PaymentTransactionId",
                table: "CardPayments",
                column: "PaymentTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPayments");
        }
    }
}
