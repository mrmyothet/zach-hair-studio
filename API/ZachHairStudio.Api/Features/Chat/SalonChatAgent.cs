using System.ClientModel;
using OpenAI.Chat;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Features.Chat;

public interface ISalonChatAgent
{
    Task<string> ReplyAsync(IReadOnlyList<ChatHistoryMessage> history, CancellationToken cancellationToken);
}

public interface IChatCompletionClient
{
    Task<ChatCompletion> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken);
}

public sealed class OpenAIChatCompletionClient(ChatClient client) : IChatCompletionClient
{
    public async Task<ChatCompletion> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        ClientResult<ChatCompletion> result = await client.CompleteChatAsync(messages, options, cancellationToken);
        return result.Value;
    }
}

public sealed class SalonChatAgent : ISalonChatAgent
{
    private readonly IChatCompletionClient _client;
    private readonly SalonChatTools _tools;
    private readonly HuggingFaceOptions _options;
    private readonly SalonTimeZone _salonTimeZone;
    private readonly TimeProvider _timeProvider;

    public SalonChatAgent(
        IChatCompletionClient client,
        SalonChatTools tools,
        HuggingFaceOptions options,
        SalonOptions salonOptions,
        TimeProvider timeProvider)
    {
        _client = client;
        _tools = tools;
        _options = options;
        _salonTimeZone = SalonTimeZone.FromOptions(salonOptions);
        _timeProvider = timeProvider;
    }

    public async Task<string> ReplyAsync(
        IReadOnlyList<ChatHistoryMessage> history,
        CancellationToken cancellationToken)
    {
        var localToday = DateOnly.FromDateTime(_salonTimeZone.ToSalonLocal(_timeProvider.GetUtcNow()));
        List<ChatMessage> messages =
        [
            new SystemChatMessage(BuildSystemPrompt(localToday)),
            .. history.Select(ToOpenAIMessage),
        ];
        ChatCompletionOptions completionOptions = new();
        foreach (var tool in SalonChatTools.Definitions)
        {
            completionOptions.Tools.Add(tool);
        }

        for (var round = 0; round <= _options.MaxToolRounds; round++)
        {
            var completion = await _client.CompleteAsync(messages, completionOptions, cancellationToken);
            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                if (round == _options.MaxToolRounds)
                {
                    throw new ChatAgentException("The assistant exceeded the tool-call limit.");
                }

                messages.Add(new AssistantChatMessage(completion));
                foreach (var toolCall in completion.ToolCalls)
                {
                    var result = await _tools.ExecuteAsync(
                        toolCall.FunctionName,
                        toolCall.FunctionArguments,
                        cancellationToken);
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                }
                continue;
            }

            if (completion.FinishReason != ChatFinishReason.Stop)
            {
                throw new ChatAgentException($"The model stopped unexpectedly ({completion.FinishReason}).");
            }

            var reply = string.Concat(completion.Content.Select(part => part.Text)).Trim();
            return !string.IsNullOrWhiteSpace(reply)
                ? reply
                : throw new ChatAgentException("The model returned an empty response.");
        }

        throw new ChatAgentException("The assistant could not complete the request.");
    }

    private static ChatMessage ToOpenAIMessage(ChatHistoryMessage message) =>
        message.Role == "user"
            ? new UserChatMessage(message.Content)
            : new AssistantChatMessage(message.Content);

    private static string BuildSystemPrompt(DateOnly today) => $$"""
        You are Zach Hair Studio's concise staff assistant. Today in the salon is {{today:yyyy-MM-dd}}.
        Answer salon-data questions only from tool results. Never invent services, stylists, ids, bookings, or openings.
        Before choosing a service from a descriptive phrase such as "haircut", call list_services and semantically select the closest real catalog entry. If no confident match exists, ask the user to clarify.
        Before choosing a named stylist, call list_stylists. Use get_appointment_slots only with exact ids returned by those tools.
        Use list_bookings for staff schedule questions. Never reveal or request contact details.
        Resolve relative dates against today's salon-local date. Keep answers brief and use site-relative links such as [Open the schedule](/schedule) when helpful.
        All tools are read-only. Do not claim to create, cancel, or change an appointment.
        """;
}

public sealed class ChatAgentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
