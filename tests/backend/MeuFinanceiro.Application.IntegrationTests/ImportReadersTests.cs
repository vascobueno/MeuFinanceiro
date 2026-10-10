using FluentAssertions;
using MeuFinanceiro.Application.Reconciliation.Import;
using Xunit;

namespace MeuFinanceiro.Application.IntegrationTests;

/// <summary>
/// Leitores de texto colado de extrato e fatura.
///
/// Os textos imitam a ESTRUTURA de extratos e faturas reais de dois bancos (sinal no fim do
/// valor, data só na primeira linha do dia, descrição em duas linhas, parcela em três formatos,
/// compra em dólar com IOF à parte), mas todo o conteúdo — portadores, lojas, finais, valores —
/// é fictício.
/// </summary>
[Trait("Category", "Unit")]
public class ImportReadersTests
{
    [Theory]
    [InlineData("1.288,46-", -1288.46)]
    [InlineData("-1.288,46", -1288.46)]
    [InlineData("-R$ 54,00", -54.00)]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("R$ -10,00", -10.00)]
    [InlineData("25,00", 25.00)]
    [InlineData("0,01", 0.01)]
    [InlineData("-1288.46", -1288.46)]
    [InlineData("10", 10)]
    public void Deve_ler_valores_nos_formatos_dos_bancos(string bruto, double esperado)
    {
        ImportValueParser.TryParseMoney(bruto, out var valor).Should().BeTrue();
        valor.Should().Be((decimal)esperado);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("")]
    public void Deve_recusar_o_que_nao_e_valor(string bruto)
    {
        ImportValueParser.TryParseMoney(bruto, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("200652", false)]   // número de documento
    [InlineData("04/06", false)]    // parcela
    [InlineData("5,4970", false)]   // cotação, quatro casas
    [InlineData("1.288,46-", true)]
    [InlineData("-R$54,00", true)]
    public void So_e_valor_o_token_com_duas_casas_decimais(string token, bool esperado)
    {
        ImportValueParser.IsMoneyToken(token).Should().Be(esperado);
    }

    [Theory]
    [InlineData("02/01", "2023-01-31", "2023-01-02")]
    [InlineData("31/12", "2023-01-31", "2022-12-31")]   // virada de ano no extrato de janeiro
    [InlineData("19/05", "2026-09-02", "2026-05-19")]   // compra parcelada meses antes
    [InlineData("02/01/2023", "2026-09-02", "2023-01-02")]
    [InlineData("2023-01-02", "2026-09-02", "2023-01-02")]
    public void Deve_dar_ano_a_data_pela_referencia(string bruto, string referencia, string esperado)
    {
        ImportValueParser.TryParseDate(bruto, DateOnly.Parse(referencia), out var data).Should().BeTrue();
        data.Should().Be(DateOnly.Parse(esperado));
    }

    [Fact]
    public void Deve_recusar_data_impossivel()
    {
        ImportValueParser.TryParseDate("32/01", new DateOnly(2023, 1, 31), out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------ extrato

    private const string ExtratoNoFormatoDoBanco = """
        SALDO EM 31/12 1.782,59
        02/01 COMPRA CARTAO DEB MC 200652 18,00-
        02/01 LOJA FICTICIA A
        COMPRA CARTAO DEB MC 232552 10,50- 1.754,09
        02/01 LOJA FICTICIA B
        Data Descrição Nº Documento Movimento (R$) Saldo (R$)
        11/01 PAGAMENTO DE BOLETO OUTROS BANCOS - 1.288,46-
        PIX RECEBIDO 259,76
        PORTADOR FICTICIO
        SALDO EM 31/01 725,39
        """;

    [Fact]
    public void Extrato_deve_ler_movimentos_no_formato_do_banco()
    {
        var r = StatementTextReader.Read(ExtratoNoFormatoDoBanco, new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31));

        r.Errors.Should().BeEmpty();
        r.Items.Should().HaveCount(4);

        r.Items[0].Date.Should().Be(new DateOnly(2023, 1, 2));
        r.Items[0].Amount.Should().Be(-18.00m);
        r.Items[0].Document.Should().Be("200652");
        // A segunda linha (estabelecimento) é continuação, não outro movimento.
        r.Items[0].Description.Should().Be("COMPRA CARTAO DEB MC 02/01 LOJA FICTICIA A");

        // Sem data na linha: herda a do movimento anterior. O segundo valor é o saldo do dia.
        r.Items[1].Date.Should().Be(new DateOnly(2023, 1, 2));
        r.Items[1].Amount.Should().Be(-10.50m);

        // Documento "-" é "sem documento"; o cabeçalho de página no meio não vira descrição.
        r.Items[2].Document.Should().BeNull();
        r.Items[2].Amount.Should().Be(-1288.46m);
        r.Items[2].Description.Should().Be("PAGAMENTO DE BOLETO OUTROS BANCOS");

        r.Items[3].Amount.Should().Be(259.76m);
        r.Items[3].Description.Should().Be("PIX RECEBIDO PORTADOR FICTICIO");
        r.Items[3].ItemType.Should().Be("OTHER_INCOME");

        r.OpeningBalance.Should().Be(1782.59m);
        r.ClosingBalance.Should().Be(725.39m);
        r.MovementsSum.Should().Be(-1057.20m);
    }

    [Fact]
    public void Extrato_deve_por_31_12_no_ano_anterior_num_extrato_de_janeiro()
    {
        var r = StatementTextReader.Read("31/12 TARIFA MENSALIDADE PACOTE SERVICOS 96,35-",
            new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31));

        r.Items.Single().Date.Should().Be(new DateOnly(2022, 12, 31));
    }

    [Fact]
    public void Extrato_continua_aceitando_o_formato_antigo_com_ponto_e_virgula()
    {
        var r = StatementTextReader.Read("2026-05-20;Aplicação CDB;INVESTMENT;-10000.00",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        r.Errors.Should().BeEmpty();
        r.Items.Single().ItemType.Should().Be("INVESTMENT");
        r.Items.Single().Amount.Should().Be(-10000m);
    }

    [Fact]
    public void Extrato_deve_explicar_qual_linha_falhou_e_por_que()
    {
        var r = StatementTextReader.Read("""
            02/01 COMPRA SEM VALOR NENHUM
            40/01 LOJA 10,00-
            a;b
            """, new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 31));

        r.Errors.Select(e => e.Line).Should().Equal(1, 2, 3);
        r.Errors[0].Message.Should().Contain("sem valor");
        r.Errors[1].Message.Should().Contain("Data inválida");
        r.Errors[2].Message.Should().Contain("Formato não reconhecido");
    }

    // ------------------------------------------------------------------ fatura

    private const string FaturaComSecoes = """
        PORTADOR TITULAR - 5480 XXXX XXXX 1111
        Pagamento e Demais Créditos
        Compra Data Descrição Parcela R$ US$
        10/08 PAGAMENTO DE FATURA-INTERNET -9.195,78
        Parcelamentos
        1 19/05 LOJA PARCELADA 04/06 788,52
        Despesas
        3 06/08 LOJA FICTICIA A 58,50
        21/08 SERVICO NO EXTERIOR 116,65 21,22
        COTAÇÃO DOLAR R$ 5,4970
        IOF DESPESA NO EXTERIOR 4,08
        VALOR TOTAL 1.957,94 0,00
        02/09 ANUIDADE DIFERENCIADA 08/12 0,00
        """;

    [Fact]
    public void Fatura_deve_ler_itens_e_ignorar_totais_e_cabecalhos()
    {
        var r = InvoiceTextReader.Read(FaturaComSecoes, new DateOnly(2026, 9, 2));

        r.Errors.Should().BeEmpty();
        r.Items.Select(i => i.Description).Should().Equal(
            "PAGAMENTO DE FATURA-INTERNET", "LOJA PARCELADA", "LOJA FICTICIA A",
            "SERVICO NO EXTERIOR", "IOF DESPESA NO EXTERIOR", "ANUIDADE DIFERENCIADA");

        r.Items[0].Amount.Should().Be(-9195.78m);
        // A parcela vem com a data da compra original, meses antes do fechamento.
        r.Items[1].Date.Should().Be(new DateOnly(2026, 5, 19));
        r.Items[1].InstallmentLabel.Should().Be("04/06");
        r.Items[1].Amount.Should().Be(788.52m);
        // O segundo valor é o em dólar: não pode ser lido como reais.
        r.Items[3].Amount.Should().Be(116.65m);
        // O IOF não tem data: herda a da compra no exterior.
        r.Items[4].Date.Should().Be(new DateOnly(2026, 8, 21));
        r.Items[5].InstallmentLabel.Should().Be("08/12");
        r.Items[5].Amount.Should().Be(0m);
    }

    [Fact]
    public void Fatura_deve_ler_a_parcela_nos_tres_formatos()
    {
        var r = InvoiceTextReader.Read("""
            10/08 Pagto Boleto Recebido -R$ 54,00
            20/02 Loja Online 328083 Parcela 02/02 R$ 285,00
            03/09 Anuidade Diferenc.titular05-12 R$ 36,00
            Total de compras e despesas nesse cartão: R$ 36,00
            """, new DateOnly(2026, 9, 3));

        r.Errors.Should().BeEmpty();
        r.Items.Should().HaveCount(3);
        r.Items[0].Amount.Should().Be(-54.00m);
        r.Items[1].InstallmentLabel.Should().Be("02/02");
        r.Items[1].Description.Should().Be("Loja Online 328083");
        r.Items[2].InstallmentLabel.Should().Be("05/12");
        r.Items[2].Description.Should().Be("Anuidade Diferenc.titular");
    }

    [Fact]
    public void Fatura_deve_separar_duas_colunas_na_mesma_linha()
    {
        var r = InvoiceTextReader.Read(
            "12/03 Pagto Boleto Recebido -R$ 2.462,51    14/03 Estacionamento Ficticio R$ 2,90",
            new DateOnly(2026, 4, 1));

        r.Items.Should().HaveCount(2);
        r.Items[0].Amount.Should().Be(-2462.51m);
        r.Items[1].Description.Should().Be("Estacionamento Ficticio");
        r.Items[1].Amount.Should().Be(2.90m);
    }

    [Theory]
    [InlineData("04/06", 4, 6)]
    [InlineData("Parcela 10/12", 10, 12)]
    [InlineData("04-06", 4, 6)]
    [InlineData(" 1 / 3 ", 1, 3)]
    public void Rotulo_de_parcela_vira_numero_e_total(string label, int number, int total)
    {
        InstallmentLabel.TryParse(label).Should().Be((number, total));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("07/06")]
    [InlineData("00/06")]
    [InlineData("LOJA 04/06")]
    [InlineData("02/01/2026")]
    public void Rotulo_que_nao_e_parcela_nao_vira_nada(string? label)
    {
        InstallmentLabel.TryParse(label).Should().BeNull();
    }
}
