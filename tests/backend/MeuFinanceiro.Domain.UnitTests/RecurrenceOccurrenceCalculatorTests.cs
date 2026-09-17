using FluentAssertions;
using MeuFinanceiro.Application.Recurring;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Xunit;

namespace MeuFinanceiro.Domain.UnitTests;

/// <summary>
/// Cobre docs/20-testes.md — datas de ocorrência de recorrência (regra 13 / docs/07).
///
/// Testes de regressão do bug em que as ocorrências eram geradas avançando de uma para a
/// seguinte: como AddMonths encurta a data em meses mais curtos, o encurtamento se acumulava e
/// uma conta que vencia todo dia 31 ficava presa no dia 28 depois do primeiro fevereiro.
/// </summary>
[Trait("Category", "Unit")]
public class RecurrenceOccurrenceCalculatorTests
{
    private static RecurringTransaction Recorrencia(string startDate, RecurrenceFrequency frequency, string? endDate = null) =>
        new()
        {
            StartDate = DateOnly.Parse(startDate),
            Frequency = frequency,
            EndDate = endDate is null ? null : DateOnly.Parse(endDate),
            IsActive = true
        };

    private static DateOnly Data(string value) => DateOnly.Parse(value);

    // ---------------- fim de mês (a regressão principal) ----------------

    [Fact]
    public void Mensal_iniciada_no_dia_31_deve_voltar_ao_dia_31_nos_meses_que_permitem()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2026-01-31", RecurrenceFrequency.Monthly), Data("2026-01-31"), 5);

        // 28/02 é o encurtamento legítimo de fevereiro; o que NÃO pode acontecer é março e maio
        // herdarem o dia 28 — era exatamente o bug.
        ocorrencias.Should().Equal(
            Data("2026-02-28"),
            Data("2026-03-31"),
            Data("2026-04-30"),
            Data("2026-05-31"),
            Data("2026-06-30"));
    }

    [Fact]
    public void Mensal_iniciada_no_dia_30_deve_encurtar_so_em_fevereiro()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2026-01-30", RecurrenceFrequency.Monthly), Data("2026-01-30"), 3);

        ocorrencias.Should().Equal(Data("2026-02-28"), Data("2026-03-30"), Data("2026-04-30"));
    }

    [Fact]
    public void Anual_iniciada_em_29_de_fevereiro_deve_voltar_ao_dia_29_no_proximo_bissexto()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2024-02-29", RecurrenceFrequency.Yearly), Data("2024-02-29"), 4);

        ocorrencias.Should().Equal(
            Data("2025-02-28"),
            Data("2026-02-28"),
            Data("2027-02-28"),
            Data("2028-02-29"));
    }

    // ---------------- semanal ----------------

    [Fact]
    public void Semanal_deve_avancar_de_sete_em_sete_dias()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2026-01-01", RecurrenceFrequency.Weekly), Data("2026-01-01"), 3);

        ocorrencias.Should().Equal(Data("2026-01-08"), Data("2026-01-15"), Data("2026-01-22"));
    }

    [Fact]
    public void Semanal_antiga_deve_manter_o_dia_da_semana_da_data_inicial()
    {
        // 01/01/2023 é domingo; 15/09/2026 é terça. A próxima ocorrência é o domingo seguinte,
        // não o dia seguinte a hoje.
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2023-01-01", RecurrenceFrequency.Weekly), Data("2026-09-15"), 2);

        ocorrencias.Should().Equal(Data("2026-09-20"), Data("2026-09-27"));
        ocorrencias.Should().OnlyContain(d => d.DayOfWeek == DayOfWeek.Sunday);
    }

    // ---------------- ponto de partida ----------------

    [Fact]
    public void Recorrencia_antiga_deve_comecar_na_primeira_ocorrencia_futura()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2023-01-31", RecurrenceFrequency.Monthly), Data("2026-09-15"), 2);

        ocorrencias.Should().Equal(Data("2026-09-30"), Data("2026-10-31"));
    }

    [Fact]
    public void Recorrencia_que_ainda_nao_comecou_deve_partir_da_ocorrencia_seguinte_a_inicial()
    {
        // A ocorrência da própria StartDate já virou lançamento ao criar a recorrência.
        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2027-01-31", RecurrenceFrequency.Monthly), Data("2026-09-15"), 2);

        ocorrencias.Should().Equal(Data("2027-02-28"), Data("2027-03-31"));
    }

    [Fact]
    public void NextOccurrences_nunca_deve_devolver_data_passada()
    {
        var hoje = Data("2026-09-15");

        var ocorrencias = RecurrenceOccurrenceCalculator
            .NextOccurrences(Recorrencia("2019-03-31", RecurrenceFrequency.Monthly), hoje, 6);

        ocorrencias.Should().OnlyContain(d => d > hoje);
    }

    // ---------------- janela e vigência ----------------

    [Fact]
    public void EnumerateUntil_deve_respeitar_a_data_limite()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .EnumerateUntil(Recorrencia("2026-01-31", RecurrenceFrequency.Monthly), Data("2026-03-15"), Data("2026-06-30"));

        ocorrencias.Should().Equal(
            Data("2026-03-31"),
            Data("2026-04-30"),
            Data("2026-05-31"),
            Data("2026-06-30"));
    }

    [Fact]
    public void EnumerateUntil_deve_parar_na_EndDate()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .EnumerateUntil(Recorrencia("2026-01-31", RecurrenceFrequency.Monthly, "2026-04-30"), Data("2026-03-15"), Data("2026-12-31"));

        ocorrencias.Should().Equal(Data("2026-03-31"), Data("2026-04-30"));
    }

    [Fact]
    public void EnumerateUntil_deve_ser_vazio_quando_a_janela_termina_antes_da_proxima_ocorrencia()
    {
        var ocorrencias = RecurrenceOccurrenceCalculator
            .EnumerateUntil(Recorrencia("2026-01-15", RecurrenceFrequency.Monthly), Data("2026-03-16"), Data("2026-04-10"));

        ocorrencias.Should().BeEmpty();
    }

    // ---------------- custo ----------------

    [Fact]
    public void Custo_nao_deve_crescer_com_a_idade_da_recorrencia()
    {
        // Antes, achar a primeira ocorrência futura era um laço período a período desde a
        // StartDate: uma semanal de 10 anos atrás custava ~522 iterações inúteis por recorrência.
        var antiga = Recorrencia("2016-01-01", RecurrenceFrequency.Weekly);
        var hoje = Data("2026-09-15");

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5_000; i++)
            _ = RecurrenceOccurrenceCalculator.NextOccurrences(antiga, hoje, 3).ToList();
        cronometro.Stop();

        // Margem larga de propósito: o que interessa é detectar a volta do laço linear, não
        // cravar um número de máquina.
        cronometro.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
