using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "RecurringTransactions",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Reconciliations",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Merchants",
                newName: "HouseholdId");

            migrationBuilder.RenameIndex(
                name: "IX_Merchants_UserId_Name",
                table: "Merchants",
                newName: "IX_Merchants_HouseholdId_Name");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "InvestmentAccounts",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "InstallmentPlans",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ImportFiles",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "FinancialTransactions",
                newName: "HouseholdId");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_UserId_TransactionDate",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_HouseholdId_TransactionDate");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_UserId_Description",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_HouseholdId_Description");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "CreditCards",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Categories",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "CardInvoices",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "BankStatements",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Accounts",
                newName: "HouseholdId");

            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            // Backfill: cada usuário existente vira dono de um household novo, reaproveitando o
            // próprio Id do usuário como Id do household — assim os valores já renomeados de
            // UserId->HouseholdId nas 12 tabelas acima já apontam certo, sem UPDATE linha a linha.
            migrationBuilder.Sql(@"
                INSERT INTO ""Households"" (""Id"", ""Name"", ""CreatedAt"")
                SELECT ""Id"", 'Família de ' || ""Name"", now()
                FROM ""AspNetUsers"";
            ");

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill: todo usuário existente vira Owner do household criado acima pra ele.
            migrationBuilder.Sql(@"
                INSERT INTO ""HouseholdMembers"" (""Id"", ""HouseholdId"", ""UserId"", ""Role"", ""CreatedAt"")
                SELECT gen_random_uuid(), ""Id"", ""Id"", 0, now()
                FROM ""AspNetUsers"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_HouseholdId",
                table: "RecurringTransactions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_HouseholdId",
                table: "Reconciliations",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentAccounts_HouseholdId",
                table: "InvestmentAccounts",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentPlans_HouseholdId",
                table: "InstallmentPlans",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_HouseholdId",
                table: "ImportFiles",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_HouseholdId",
                table: "CreditCards",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId",
                table: "Categories",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_CardInvoices_HouseholdId",
                table: "CardInvoices",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_HouseholdId",
                table: "BankStatements",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_HouseholdId",
                table: "Accounts",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId",
                table: "HouseholdMembers",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Households_HouseholdId",
                table: "Accounts",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankStatements_Households_HouseholdId",
                table: "BankStatements",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CardInvoices_Households_HouseholdId",
                table: "CardInvoices",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCards_Households_HouseholdId",
                table: "CreditCards",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_Households_HouseholdId",
                table: "FinancialTransactions",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportFiles_Households_HouseholdId",
                table: "ImportFiles",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InstallmentPlans_Households_HouseholdId",
                table: "InstallmentPlans",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestmentAccounts_Households_HouseholdId",
                table: "InvestmentAccounts",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Merchants_Households_HouseholdId",
                table: "Merchants",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reconciliations_Households_HouseholdId",
                table: "Reconciliations",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Households_HouseholdId",
                table: "RecurringTransactions",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Households_HouseholdId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_BankStatements_Households_HouseholdId",
                table: "BankStatements");

            migrationBuilder.DropForeignKey(
                name: "FK_CardInvoices_Households_HouseholdId",
                table: "CardInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditCards_Households_HouseholdId",
                table: "CreditCards");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_Households_HouseholdId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportFiles_Households_HouseholdId",
                table: "ImportFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_InstallmentPlans_Households_HouseholdId",
                table: "InstallmentPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestmentAccounts_Households_HouseholdId",
                table: "InvestmentAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Merchants_Households_HouseholdId",
                table: "Merchants");

            migrationBuilder.DropForeignKey(
                name: "FK_Reconciliations_Households_HouseholdId",
                table: "Reconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Households_HouseholdId",
                table: "RecurringTransactions");

            migrationBuilder.DropTable(
                name: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_HouseholdId",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Reconciliations_HouseholdId",
                table: "Reconciliations");

            migrationBuilder.DropIndex(
                name: "IX_InvestmentAccounts_HouseholdId",
                table: "InvestmentAccounts");

            migrationBuilder.DropIndex(
                name: "IX_InstallmentPlans_HouseholdId",
                table: "InstallmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_ImportFiles_HouseholdId",
                table: "ImportFiles");

            migrationBuilder.DropIndex(
                name: "IX_CreditCards_HouseholdId",
                table: "CreditCards");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_CardInvoices_HouseholdId",
                table: "CardInvoices");

            migrationBuilder.DropIndex(
                name: "IX_BankStatements_HouseholdId",
                table: "BankStatements");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_HouseholdId",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "RecurringTransactions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Reconciliations",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Merchants",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Merchants_HouseholdId_Name",
                table: "Merchants",
                newName: "IX_Merchants_UserId_Name");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "InvestmentAccounts",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "InstallmentPlans",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "ImportFiles",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "FinancialTransactions",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_HouseholdId_TransactionDate",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_UserId_TransactionDate");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_HouseholdId_Description",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_UserId_Description");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "CreditCards",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Categories",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "CardInvoices",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "BankStatements",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Accounts",
                newName: "UserId");
        }
    }
}
