using FluentAssertions;
using MeuFinanceiro.Application.Imports;
using MeuFinanceiro.Domain.Enums;
using Xunit;
using static MeuFinanceiro.Application.Imports.ImportRowValidator;

namespace MeuFinanceiro.Domain.UnitTests;

/// <summary>Cobre docs/20-testes.md e docs/16-importacao-inicial.md — validação de linhas do Excel.</summary>
[Trait("Category", "Unit")]
public class ImportRowValidatorTests
{
    private static readonly IReadOnlySet<string> Accounts = new HashSet<string> { "Conta Corrente" };
    private static readonly IReadOnlySet<string> Cards = new HashSet<string> { "Cartão Nubank" };
    private static readonly IReadOnlySet<string> Categories = new HashSet<string> { "Mercado" };

    [Fact]
    public void Linha_completa_e_valida_deve_passar_sem_erros()
    {
        var row = new RawRow(1, "20/05/2026", "Supermercado", "Despesa", "Conta Corrente", "Mercado", "150,50", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Date.Should().Be(new DateOnly(2026, 5, 20));
        result.Amount.Should().Be(150.50m);
        result.Type.Should().Be(TransactionType.Expense);
        result.IsAccount.Should().BeTrue();
    }

    [Fact]
    public void Linha_com_cartao_valido_deve_marcar_IsAccount_falso()
    {
        var row = new RawRow(2, "20/05/2026", "Amazon", "Despesa", "Cartão Nubank", null, "89,90", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeTrue();
        result.IsAccount.Should().BeFalse();
    }

    [Fact]
    public void Linha_com_data_invalida_deve_falhar()
    {
        var row = new RawRow(3, "não é uma data", "X", "Despesa", "Conta Corrente", null, "10,00", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Data"));
    }

    [Fact]
    public void Linha_com_conta_desconhecida_deve_falhar()
    {
        var row = new RawRow(4, "20/05/2026", "X", "Despesa", "Conta Inexistente", null, "10,00", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("não encontrado"));
    }

    [Fact]
    public void Linha_com_categoria_desconhecida_deve_falhar()
    {
        var row = new RawRow(5, "20/05/2026", "X", "Despesa", "Conta Corrente", "Categoria Fantasma", "10,00", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Categoria"));
    }

    [Fact]
    public void Linha_com_valor_zero_deve_falhar()
    {
        var row = new RawRow(6, "20/05/2026", "X", "Despesa", "Conta Corrente", null, "0", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("zero"));
    }

    [Fact]
    public void Linha_com_tipo_invalido_deve_falhar()
    {
        var row = new RawRow(7, "20/05/2026", "X", "Investimento", "Conta Corrente", null, "10,00", null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Tipo"));
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("R$ 99,90", 99.90)]
    [InlineData("50", 50)]
    public void Deve_aceitar_diferentes_formatos_de_valor(string raw, decimal expected)
    {
        var row = new RawRow(8, "20/05/2026", "X", "Receita", "Conta Corrente", null, raw, null, null, null);

        var result = Validate(row, Accounts, Cards, Categories);

        result.Amount.Should().Be(expected);
    }
}
