namespace MeuFinanceiro.Application.Attachments;

/// <summary>
/// Decide se um arquivo enviado é mesmo de um dos tipos aceitos (PDF, JPEG, PNG, WEBP, HEIC),
/// olhando os primeiros bytes do conteúdo.
///
/// Existe porque os dois metadados que acompanham um upload — a extensão do nome e o
/// Content-Type do multipart — são **escolhidos pelo cliente**. Uma allowlist que confie neles
/// não filtra nada: basta renomear qualquer arquivo para `.pdf`. A assinatura é a única parte
/// que vem do conteúdo real, e por isso é ela que manda aqui; o Content-Type gravado no banco é
/// o deduzido, nunca o informado.
///
/// A extensão ainda é conferida, mas só contra o tipo detectado — um PNG legítimo chamado
/// `nota.pdf` é recusado por incoerência, não por suspeita do conteúdo.
/// </summary>
public static class FileSignatureValidator
{
    /// <summary>Trecho fixo esperado numa posição do cabeçalho.</summary>
    private record Segment(int Offset, byte[] Bytes);

    private record FileTypeRule(string ContentType, string[] Extensions, Segment[] Segments);

    private static byte[] Ascii(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static readonly FileTypeRule[] Rules =
    [
        new("application/pdf", [".pdf"],
            [new Segment(0, Ascii("%PDF"))]),

        new("image/jpeg", [".jpg", ".jpeg"],
            [new Segment(0, [0xFF, 0xD8, 0xFF])]),

        new("image/png", [".png"],
            [new Segment(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])]),

        // RIFF....WEBP — os 4 bytes do meio são o tamanho do arquivo, então a regra tem dois
        // segmentos em vez de um bloco contíguo.
        new("image/webp", [".webp"],
            [new Segment(0, Ascii("RIFF")), new Segment(8, Ascii("WEBP"))]),

        // HEIC é um container ISO-BMFF: "ftyp" no offset 4 e a marca do formato no 8.
        new("image/heic", [".heic", ".heif"],
            [new Segment(4, Ascii("ftyp")), new Segment(8, Ascii("heic"))]),
        new("image/heic", [".heic", ".heif"],
            [new Segment(4, Ascii("ftyp")), new Segment(8, Ascii("heix"))]),
        new("image/heic", [".heic", ".heif"],
            [new Segment(4, Ascii("ftyp")), new Segment(8, Ascii("mif1"))]),
    ];

    /// <summary>Bytes suficientes para cobrir a maior regra — basta ler isto do início do stream.</summary>
    public const int HeaderBytesNeeded = 16;

    public static IReadOnlyList<string> AllowedExtensions =>
        Rules.SelectMany(r => r.Extensions).Distinct().ToArray();

    /// <summary>
    /// Devolve o Content-Type real do conteúdo, ou null com o motivo em <paramref name="error"/>.
    /// </summary>
    public static string? Resolve(ReadOnlySpan<byte> header, string fileName, out string? error)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension))
        {
            error = "Arquivo sem extensão.";
            return null;
        }

        // Laço explícito em vez de LINQ: ReadOnlySpan é ref-like e não pode ser capturado numa lambda.
        FileTypeRule? match = null;
        foreach (var rule in Rules)
        {
            if (!Matches(header, rule)) continue;
            match = rule;
            break;
        }

        if (match is null)
        {
            error = $"O conteúdo do arquivo não é de um tipo aceito. Aceitos: {string.Join(", ", AllowedExtensions)}.";
            return null;
        }

        if (!match.Extensions.Contains(extension))
        {
            error = $"A extensão {extension} não corresponde ao conteúdo do arquivo ({match.ContentType}).";
            return null;
        }

        error = null;
        return match.ContentType;
    }

    private static bool Matches(ReadOnlySpan<byte> header, FileTypeRule rule)
    {
        foreach (var segment in rule.Segments)
        {
            var end = segment.Offset + segment.Bytes.Length;
            if (header.Length < end) return false;
            if (!header.Slice(segment.Offset, segment.Bytes.Length).SequenceEqual(segment.Bytes)) return false;
        }
        return true;
    }
}
