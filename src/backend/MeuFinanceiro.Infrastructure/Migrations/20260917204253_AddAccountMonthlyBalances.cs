using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMonthlyBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMonthlyBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CumulativeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsAnchor = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMonthlyBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountMonthlyBalances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountMonthlyBalances_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMonthlyBalances_AccountId_PeriodEnd",
                table: "AccountMonthlyBalances",
                columns: new[] { "AccountId", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountMonthlyBalances_GrupoId",
                table: "AccountMonthlyBalances",
                column: "GrupoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMonthlyBalances");
        }
    }
}
