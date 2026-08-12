using System.ClientModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZachHairStudio.Api.Features.Chat;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(
    ISalonChatAgent agent,
    HuggingFaceOptions options,
    ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Messages.Count == 0 || request.Messages[^1].Role != "user")
        {
            ModelState.AddModelError(nameof(request.Messages), "The conversation must end with a user message.");
            return ValidationProblem(ModelState);
        }

        if (request.Messages.Sum(message => message.Content.Length) > 20_000)
        {
            ModelState.AddModelError(nameof(request.Messages), "Conversation history is too long.");
            return ValidationProblem(ModelState);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        try
        {
            var reply = await agent.ReplyAsync(request.Messages, timeout.Token);
            return Ok(new ChatResponse(reply));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Salon chat provider timed out.");
            return Problem(
                title: "Salon assistant timed out.",
                detail: "Please try again.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (ClientResultException exception) when (exception.Status is 429 or >= 500)
        {
            logger.LogWarning("Salon chat provider is unavailable (HTTP {Status}).", exception.Status);
            return Problem(
                title: "Salon assistant is temporarily unavailable.",
                detail: "Please try again shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ClientResultException exception)
        {
            logger.LogWarning("Salon chat provider rejected the request (HTTP {Status}).", exception.Status);
            return Problem(
                title: "Salon assistant provider error.",
                detail: "The model could not process this request.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (ChatAgentException exception)
        {
            logger.LogWarning(exception, "Salon chat agent could not complete a request.");
            return Problem(
                title: "Salon assistant could not complete the request.",
                detail: "Please rephrase and try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
