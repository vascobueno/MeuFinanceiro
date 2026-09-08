using MeuFinanceiro.Application.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// docs/16-importacao-inicial.md: Excel -> Upload -> Validação -> Prévia -> Importação ->
/// Relatório -> Conferência.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/imports")]
public class ImportsController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB — upload validado (docs/12-seguranca.md)

    private readonly IExcelImportService _importService;

    public ImportsController(IExcelImportService importService)
    {
        _importService = importService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportFileResponse>>> GetHistory(CancellationToken ct)
        => Ok(await _importService.GetHistoryAsync(ct));

    /// <summary>Upload + validação + prévia (não cria lançamentos ainda).</summary>
    [HttpPost("excel")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<ImportPreviewResponse>> UploadAndValidate(IFormFile file, [FromQuery] bool force, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
            return BadRequest("Apenas arquivos .xlsx são aceitos.");

        await using var stream = file.OpenReadStream();
        return Ok(await _importService.UploadAndValidateAsync(stream, file.FileName, force, ct));
    }

    /// <summary>Reprocessa o arquivo já validado e efetivamente cria os lançamentos.</summary>
    [HttpPost("excel/{importFileId:guid}/confirm")]
    public async Task<ActionResult<ConfirmImportResponse>> Confirm(Guid importFileId, CancellationToken ct)
        => Ok(await _importService.ConfirmAsync(importFileId, ct));
}
