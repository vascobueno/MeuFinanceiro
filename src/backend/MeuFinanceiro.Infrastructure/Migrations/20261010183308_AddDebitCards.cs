using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeuFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDebitCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IssuedCards_CreditCards_CreditCardId",
                table: "IssuedCards");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreditCardId",
                table: "IssuedCards",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "IssuedCards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "IssuedCards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedCards_AccountId",
                table: "IssuedCards",
                column: "AccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IssuedCards_Funcao",
                table: "IssuedCards",
                sql: "\"CreditCardId\" IS NOT NULL OR \"AccountId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_IssuedCards_Accounts_AccountId",
                table: "IssuedCards",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IssuedCards_CreditCards_CreditCardId",
                table: "IssuedCards",
                column: "CreditCardId",
                principalTable: "CreditCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IssuedCards_Accounts_AccountId",
                table: "IssuedCards");

            migrationBuilder.DropForeignKey(
                name: "FK_IssuedCards_CreditCards_CreditCardId",
                table: "IssuedCards");

            migrationBuilder.DropIndex(
                name: "IX_IssuedCards_AccountId",
                table: "IssuedCards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IssuedCards_Funcao",
                table: "IssuedCards");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "IssuedCards");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "IssuedCards");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreditCardId",
                table: "IssuedCards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IssuedCards_CreditCards_CreditCardId",
                table: "IssuedCards",
                column: "CreditCardId",
                principalTable: "CreditCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
