using System.ComponentModel.DataAnnotations;
using ImAI.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ImAI.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController(IAzureChatService chatService) : ControllerBase
{
    [HttpPost("chat")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        var response = await chatService.AskAsync(request.Query, cancellationToken);
        return Content(response, "text/plain; charset=utf-8");
    }
}

public sealed class AiChatRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Query { get; init; } = string.Empty;
}
