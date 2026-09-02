using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameHouseholdToGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Households",
                newName: "Grupos");

            migrationBuilder.RenameTable(
                name: "HouseholdMembers",
                newName: "GrupoMembers");

            migrationBuilder.Sql(@"ALTER TABLE ""Grupos"" RENAME CONSTRAINT ""PK_Households"" TO ""PK_Grupos"";");
            migrationBuilder.Sql(@"ALTER TABLE ""GrupoMembers"" RENAME CONSTRAINT ""PK_HouseholdMembers"" TO ""PK_GrupoMembers"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "GrupoMembers",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdMembers_HouseholdId",
                table: "GrupoMembers",
                newName: "IX_GrupoMembers_GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "GrupoMembers",
                newName: "IX_GrupoMembers_UserId");

            migrationBuilder.Sql(@"ALTER TABLE ""GrupoMembers"" RENAME CONSTRAINT ""FK_HouseholdMembers_Households_HouseholdId"" TO ""FK_GrupoMembers_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "RecurringTransactions",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_RecurringTransactions_HouseholdId",
                table: "RecurringTransactions",
                newName: "IX_RecurringTransactions_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""RecurringTransactions"" RENAME CONSTRAINT ""FK_RecurringTransactions_Households_HouseholdId"" TO ""FK_RecurringTransactions_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Reconciliations",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_Reconciliations_HouseholdId",
                table: "Reconciliations",
                newName: "IX_Reconciliations_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""Reconciliations"" RENAME CONSTRAINT ""FK_Reconciliations_Households_HouseholdId"" TO ""FK_Reconciliations_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Merchants",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_Merchants_HouseholdId_Name",
                table: "Merchants",
                newName: "IX_Merchants_GrupoId_Name");

            migrationBuilder.Sql(@"ALTER TABLE ""Merchants"" RENAME CONSTRAINT ""FK_Merchants_Households_HouseholdId"" TO ""FK_Merchants_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "InvestmentAccounts",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_InvestmentAccounts_HouseholdId",
                table: "InvestmentAccounts",
                newName: "IX_InvestmentAccounts_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""InvestmentAccounts"" RENAME CONSTRAINT ""FK_InvestmentAccounts_Households_HouseholdId"" TO ""FK_InvestmentAccounts_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "InstallmentPlans",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_InstallmentPlans_HouseholdId",
                table: "InstallmentPlans",
                newName: "IX_InstallmentPlans_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""InstallmentPlans"" RENAME CONSTRAINT ""FK_InstallmentPlans_Households_HouseholdId"" TO ""FK_InstallmentPlans_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "ImportFiles",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportFiles_HouseholdId",
                table: "ImportFiles",
                newName: "IX_ImportFiles_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""ImportFiles"" RENAME CONSTRAINT ""FK_ImportFiles_Households_HouseholdId"" TO ""FK_ImportFiles_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "FinancialTransactions",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_HouseholdId_TransactionDate",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_GrupoId_TransactionDate");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_HouseholdId_Description",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_GrupoId_Description");

            migrationBuilder.Sql(@"ALTER TABLE ""FinancialTransactions"" RENAME CONSTRAINT ""FK_FinancialTransactions_Households_HouseholdId"" TO ""FK_FinancialTransactions_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "CreditCards",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_CreditCards_HouseholdId",
                table: "CreditCards",
                newName: "IX_CreditCards_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""CreditCards"" RENAME CONSTRAINT ""FK_CreditCards_Households_HouseholdId"" TO ""FK_CreditCards_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Categories",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_HouseholdId",
                table: "Categories",
                newName: "IX_Categories_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""Categories"" RENAME CONSTRAINT ""FK_Categories_Households_HouseholdId"" TO ""FK_Categories_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "CardInvoices",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_CardInvoices_HouseholdId",
                table: "CardInvoices",
                newName: "IX_CardInvoices_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""CardInvoices"" RENAME CONSTRAINT ""FK_CardInvoices_Households_HouseholdId"" TO ""FK_CardInvoices_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "BankStatements",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_BankStatements_HouseholdId",
                table: "BankStatements",
                newName: "IX_BankStatements_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""BankStatements"" RENAME CONSTRAINT ""FK_BankStatements_Households_HouseholdId"" TO ""FK_BankStatements_Grupos_GrupoId"";");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "AuditLogs",
                newName: "GrupoId");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "Accounts",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_HouseholdId",
                table: "Accounts",
                newName: "IX_Accounts_GrupoId");

            migrationBuilder.Sql(@"ALTER TABLE ""Accounts"" RENAME CONSTRAINT ""FK_Accounts_Households_HouseholdId"" TO ""FK_Accounts_Grupos_GrupoId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Accounts"" RENAME CONSTRAINT ""FK_Accounts_Grupos_GrupoId"" TO ""FK_Accounts_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_GrupoId",
                table: "Accounts",
                newName: "IX_Accounts_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "Accounts",
                newName: "HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "AuditLogs",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""BankStatements"" RENAME CONSTRAINT ""FK_BankStatements_Grupos_GrupoId"" TO ""FK_BankStatements_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_BankStatements_GrupoId",
                table: "BankStatements",
                newName: "IX_BankStatements_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "BankStatements",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""CardInvoices"" RENAME CONSTRAINT ""FK_CardInvoices_Grupos_GrupoId"" TO ""FK_CardInvoices_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_CardInvoices_GrupoId",
                table: "CardInvoices",
                newName: "IX_CardInvoices_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "CardInvoices",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""Categories"" RENAME CONSTRAINT ""FK_Categories_Grupos_GrupoId"" TO ""FK_Categories_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_GrupoId",
                table: "Categories",
                newName: "IX_Categories_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "Categories",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""CreditCards"" RENAME CONSTRAINT ""FK_CreditCards_Grupos_GrupoId"" TO ""FK_CreditCards_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_CreditCards_GrupoId",
                table: "CreditCards",
                newName: "IX_CreditCards_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "CreditCards",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""FinancialTransactions"" RENAME CONSTRAINT ""FK_FinancialTransactions_Grupos_GrupoId"" TO ""FK_FinancialTransactions_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_GrupoId_TransactionDate",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_HouseholdId_TransactionDate");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_GrupoId_Description",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_HouseholdId_Description");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "FinancialTransactions",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""ImportFiles"" RENAME CONSTRAINT ""FK_ImportFiles_Grupos_GrupoId"" TO ""FK_ImportFiles_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_ImportFiles_GrupoId",
                table: "ImportFiles",
                newName: "IX_ImportFiles_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "ImportFiles",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""InstallmentPlans"" RENAME CONSTRAINT ""FK_InstallmentPlans_Grupos_GrupoId"" TO ""FK_InstallmentPlans_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_InstallmentPlans_GrupoId",
                table: "InstallmentPlans",
                newName: "IX_InstallmentPlans_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "InstallmentPlans",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""InvestmentAccounts"" RENAME CONSTRAINT ""FK_InvestmentAccounts_Grupos_GrupoId"" TO ""FK_InvestmentAccounts_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_InvestmentAccounts_GrupoId",
                table: "InvestmentAccounts",
                newName: "IX_InvestmentAccounts_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "InvestmentAccounts",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""Merchants"" RENAME CONSTRAINT ""FK_Merchants_Grupos_GrupoId"" TO ""FK_Merchants_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_Merchants_GrupoId_Name",
                table: "Merchants",
                newName: "IX_Merchants_HouseholdId_Name");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "Merchants",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""Reconciliations"" RENAME CONSTRAINT ""FK_Reconciliations_Grupos_GrupoId"" TO ""FK_Reconciliations_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_Reconciliations_GrupoId",
                table: "Reconciliations",
                newName: "IX_Reconciliations_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "Reconciliations",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""RecurringTransactions"" RENAME CONSTRAINT ""FK_RecurringTransactions_Grupos_GrupoId"" TO ""FK_RecurringTransactions_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_RecurringTransactions_GrupoId",
                table: "RecurringTransactions",
                newName: "IX_RecurringTransactions_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "RecurringTransactions",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""GrupoMembers"" RENAME CONSTRAINT ""FK_GrupoMembers_Grupos_GrupoId"" TO ""FK_HouseholdMembers_Households_HouseholdId"";");

            migrationBuilder.RenameIndex(
                name: "IX_GrupoMembers_UserId",
                table: "GrupoMembers",
                newName: "IX_HouseholdMembers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_GrupoMembers_GrupoId",
                table: "GrupoMembers",
                newName: "IX_HouseholdMembers_HouseholdId");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "GrupoMembers",
                newName: "HouseholdId");

            migrationBuilder.Sql(@"ALTER TABLE ""GrupoMembers"" RENAME CONSTRAINT ""PK_GrupoMembers"" TO ""PK_HouseholdMembers"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Grupos"" RENAME CONSTRAINT ""PK_Grupos"" TO ""PK_Households"";");

            migrationBuilder.RenameTable(
                name: "GrupoMembers",
                newName: "HouseholdMembers");

            migrationBuilder.RenameTable(
                name: "Grupos",
                newName: "Households");
        }
    }
}
