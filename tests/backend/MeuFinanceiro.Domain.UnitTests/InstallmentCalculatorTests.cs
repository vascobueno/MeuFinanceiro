using FluentAssertions;
using MeuFinanceiro.Application.Installments;
using Xunit;

namespace MeuFinanceiro.Domain.UnitTests;

/// <summary>Cobre docs/20-testes.md — cálculo de parcelas e arredondamento (regras 15/16/17).</summary>
[Trait("Category", "Unit")]
public class InstallmentCalculatorTests
{
    [Fact]
    public void SplitTotal_1000_em_3_deve_fechar_exatamente_o_principal()
    {
        var result = InstallmentCalculator.SplitTotal(1000m, 3);

        result.Should().Equal(333.33m, 333.33m, 333.34m);
        result.Sum().Should().Be(1000m);
    }

    [Fact]
    public void SplitTotal_com_1_parcela_deve_retornar_o_valor_total()
    {
        var result = InstallmentCalculator.SplitTotal(1500.75m, 1);

        result.Should().ContainSingle().Which.Should().Be(1500.75m);
    }

    [Fact]
    public void SplitTotal_deve_lancar_para_quantidade_invalida()
    {
        var act = () => InstallmentCalculator.SplitTotal(100m, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(100, 6)]
    [InlineData(999.99, 7)]
    [InlineData(0.10, 3)]
    [InlineData(10000, 24)]
    public void SplitTotal_soma_das_parcelas_deve_sempre_fechar_o_total(decimal total, int count)
    {
        var result = InstallmentCalculator.SplitTotal(total, count);

        result.Should().HaveCount(count);
        result.Sum().Should().Be(total);
    }

    [Fact]
    public void FromInstallmentAmount_250_x_12_deve_totalizar_3000()
    {
        var result = InstallmentCalculator.FromInstallmentAmount(250m, 12);

        result.Should().HaveCount(12);
        result.Should().OnlyContain(v => v == 250m);
        result.Sum().Should().Be(3000m);
    }

    [Fact]
    public void FromInstallmentAmount_deve_lancar_para_quantidade_invalida()
    {
        var act = () => InstallmentCalculator.FromInstallmentAmount(100m, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
