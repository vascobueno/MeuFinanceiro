using FluentAssertions;
using MeuFinanceiro.Application.Cards;
using Xunit;

namespace MeuFinanceiro.Domain.UnitTests;

/// <summary>
/// Cobre a regra de ciclo de fatura (docs/05, seção Pagamento de cartão).
///
/// O lote que importa é o de fim de mês: um fechamento dia 31 tem de virar 28 em fevereiro e
/// VOLTAR ao 31 em março. Calcular avançando de ciclo em ciclo acumularia o encurtamento e
/// prenderia o cartão no dia 28 — o mesmo bug que já corrigimos nas recorrências.
/// </summary>
[Trait("Category", "Unit")]
public class CardCycleCalculatorTests
{
    private static DateOnly D(string value) => DateOnly.Parse(value);

    // ---------------- fim de mês (a regressão principal) ----------------

    [Fact]
    public void Fechamento_dia_31_deve_voltar_ao_dia_31_nos_meses_que_permitem()
    {
        var fechamentos = Enumerable.Range(1, 5)
            .Select(mes => CardCycleCalculator.CycleForMonth(31, 10, new DateOnly(2026, mes, 1)).ClosingDate)
            .ToList();

        fechamentos.Should().Equal(
            D("2026-01-31"),
            D("2026-02-28"),   // encurtamento legítimo
            D("2026-03-31"),   // ...e a volta, que era o bug
            D("2026-04-30"),
            D("2026-05-31"));
    }

    [Fact]
    public void Fechamento_dia_31_em_ano_bissexto_deve_cair_em_29_de_fevereiro()
    {
        CardCycleCalculator.CycleForMonth(31, 10, D("2028-02-15")).ClosingDate
            .Should().Be(D("2028-02-29"));
    }

    [Fact]
    public void Shift_repetido_nao_deve_grudar_no_dia_encurtado()
    {
        // Doze passos a partir de janeiro: se Shift somasse período em vez de recalcular pelo mês,
        // a partir de fevereiro tudo ficaria preso no dia 28.
        var ciclo = CardCycleCalculator.CycleForMonth(31, 10, D("2026-01-15"));

        for (var i = 0; i < 12; i++)
            ciclo = CardCycleCalculator.Shift(31, 10, ciclo, 1);

        ciclo.ClosingDate.Should().Be(D("2027-01-31"));
    }

    // ---------------- período do ciclo ----------------

    [Fact]
    public void Inicio_do_ciclo_deve_ser_o_dia_seguinte_ao_fechamento_anterior()
    {
        var ciclo = CardCycleCalculator.CycleForMonth(5, 15, D("2026-03-20"));

        ciclo.Start.Should().Be(D("2026-02-06"));
        ciclo.ClosingDate.Should().Be(D("2026-03-05"));
    }

    [Fact]
    public void Inicio_apos_fevereiro_curto_deve_acompanhar_o_fechamento_encurtado()
    {
        var ciclo = CardCycleCalculator.CycleForMonth(31, 10, D("2026-03-10"));

        ciclo.Start.Should().Be(D("2026-03-01"));      // dia seguinte a 28/02
        ciclo.ClosingDate.Should().Be(D("2026-03-31"));
    }

    // ---------------- vencimento ----------------

    [Fact]
    public void Vencimento_maior_que_fechamento_deve_cair_no_mesmo_mes()
    {
        CardCycleCalculator.CycleForMonth(5, 15, D("2026-03-01")).DueDate
            .Should().Be(D("2026-03-15"));
    }

    [Fact]
    public void Vencimento_menor_que_fechamento_deve_cair_no_mes_seguinte()
    {
        CardCycleCalculator.CycleForMonth(25, 10, D("2026-03-01")).DueDate
            .Should().Be(D("2026-04-10"));
    }

    [Fact]
    public void Vencimento_igual_ao_fechamento_deve_cair_no_mes_seguinte()
    {
        // "Próxima ocorrência DEPOIS do fechamento" — no mesmo dia não serve.
        CardCycleCalculator.CycleForMonth(10, 10, D("2026-03-01")).DueDate
            .Should().Be(D("2026-04-10"));
    }

    [Fact]
    public void Vencimento_dia_31_deve_encurtar_no_mes_curto()
    {
        CardCycleCalculator.CycleForMonth(15, 31, D("2026-04-01")).DueDate
            .Should().Be(D("2026-04-30"));
    }

    // ---------------- pertencimento ----------------

    [Fact]
    public void Compra_no_dia_do_fechamento_deve_entrar_no_ciclo_que_fecha()
    {
        CardCycleCalculator.CycleOf(5, 15, D("2026-03-05")).ClosingDate
            .Should().Be(D("2026-03-05"));
    }

    [Fact]
    public void Compra_um_dia_depois_do_fechamento_deve_cair_no_ciclo_seguinte()
    {
        CardCycleCalculator.CycleOf(5, 15, D("2026-03-06")).ClosingDate
            .Should().Be(D("2026-04-05"));
    }

    [Fact]
    public void Compra_deve_sempre_cair_dentro_da_janela_do_ciclo_devolvido()
    {
        // Varre um ano inteiro com fechamento 31 — nenhuma data pode ficar fora do próprio ciclo.
        for (var dia = D("2026-01-01"); dia <= D("2026-12-31"); dia = dia.AddDays(1))
        {
            var ciclo = CardCycleCalculator.CycleOf(31, 10, dia);
            dia.Should().BeOnOrAfter(ciclo.Start, "a data {0} caiu antes do início do ciclo", dia);
            dia.Should().BeOnOrBefore(ciclo.ClosingDate, "a data {0} caiu depois do fechamento", dia);
        }
    }

    [Fact]
    public void Ciclos_consecutivos_nao_devem_deixar_buraco_nem_sobrepor()
    {
        var atual = CardCycleCalculator.CycleForMonth(31, 10, D("2026-01-15"));

        for (var i = 0; i < 24; i++)
        {
            var proximo = CardCycleCalculator.Shift(31, 10, atual, 1);
            proximo.Start.Should().Be(atual.ClosingDate.AddDays(1),
                "o ciclo seguinte a {0} tem de começar no dia seguinte ao fechamento", atual.ClosingDate);
            atual = proximo;
        }
    }

    [Fact]
    public void Shift_negativo_deve_voltar_ao_ciclo_anterior()
    {
        var marco = CardCycleCalculator.CycleForMonth(31, 10, D("2026-03-10"));

        CardCycleCalculator.Shift(31, 10, marco, -1).ClosingDate.Should().Be(D("2026-02-28"));
    }
}
