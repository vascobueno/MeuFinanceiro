using MeuFinanceiro.Application.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeuFinanceiro.Api.Controllers;

/// <summary>
/// Anexos de lançamento e de parcelamento (docs/06, docs/07). Nota fiscal, boleto, comprovante.
///
/// Um controller só, com o dono em query param, em vez de rotas aninhadas em TransactionsController
/// e InstallmentPlansController: a validação sensível (grupo, assinatura do arquivo, limites) fica
/// num ponto único de revisão — que num endpoint que serve arquivo é exatamente o que se quer.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> GetForOwner(
        [FromQuery] Guid? transactionId, [FromQuery] Guid? installmentPlanId, CancellationToken ct)
    {
        if (!TryResolveOwner(transactionId, installmentPlanId, out var owner, out var error))
            return BadRequest(error);

        return Ok(await _attachmentService.GetForOwnerAsync(owner!, ct));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentService.MaxFileSizeBytes)]
    public async Task<ActionResult<AttachmentResponse>> Upload(
        IFormFile file, [FromQuery] Guid? transactionId, [FromQuery] Guid? installmentPlanId, CancellationToken ct)
    {
        if (!TryResolveOwner(transactionId, installmentPlanId, out var owner, out var error))
            return BadRequest(error);

        if (file is null || file.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        await using var stream = file.OpenReadStream();
        return Ok(await _attachmentService.UploadAsync(owner!, stream, file.FileName, ct));
    }

    /// <summary>
    /// Baixa o conteúdo. O service resolve o anexo restrito ao grupo do usuário (não existe query
    /// filter global no projeto), e a resposta vai sempre como download, nunca inline: servir
    /// arquivo enviado por usuário para o navegador renderizar é o caminho clássico de XSS
    /// armazenado. O nosniff impede o navegador de ignorar o Content-Type declarado.
    /// </summary>
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await _attachmentService.DownloadAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _attachmentService.DeleteAsync(id, ct);
        return NoContent();
    }

    private static bool TryResolveOwner(Guid? transactionId, Guid? installmentPlanId, out AttachmentOwner? owner, out string? error)
    {
        owner = null;
        error = null;

        if (transactionId.HasValue == installmentPlanId.HasValue)
        {
            error = "Informe exatamente um entre transactionId e installmentPlanId.";
            return false;
        }

        owner = transactionId.HasValue
            ? new AttachmentOwner(AttachmentOwnerKind.Transaction, transactionId.Value)
            : new AttachmentOwner(AttachmentOwnerKind.InstallmentPlan, installmentPlanId!.Value);
        return true;
    }
}
