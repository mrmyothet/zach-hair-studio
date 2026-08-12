using OpenAI.Chat;
using ZachHairStudio.Api.Features.Chat;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.Features.Chat;

public class SalonChatAgentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SalonChatAgentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReplyAsync_ExecutesEveryToolCallAndCorrelatesResults()
    {
        var first = Completion(
            ChatFinishReason.ToolCalls,
            toolCalls:
            [
                ChatToolCall.CreateFunctionToolCall("services-call", SalonChatTools.ListServicesName, BinaryData.FromString("{}")),
                ChatToolCall.CreateFunctionToolCall("stylists-call", SalonChatTools.ListStylistsName, BinaryData.FromString("{}")),
            ]);
        var client = new QueueChatClient(first, Completion(ChatFinishReason.Stop, "Precision Cut is available with Zin Min."));
        using var scope = _factory.Services.CreateScope();
        var agent = CreateAgent(client, scope.ServiceProvider.GetRequiredService<SalonChatTools>());

        var reply = await agent.ReplyAsync(
            [new ChatHistoryMessage { Role = "user", Content = "Who can do a haircut?" }],
            CancellationToken.None);

        Assert.Equal("Precision Cut is available with Zin Min.", reply);
        Assert.Equal(2, client.Requests.Count);
        Assert.All(client.Requests, request => Assert.Equal(4, request.ToolCount));

        var secondRequest = client.Requests[1].Messages;
        var assistant = Assert.IsType<AssistantChatMessage>(secondRequest[^3]);
        Assert.Equal(["services-call", "stylists-call"], assistant.ToolCalls.Select(call => call.Id));

        var serviceResult = Assert.IsType<ToolChatMessage>(secondRequest[^2]);
        Assert.Contains("Precision Cut", Text(serviceResult));

        var stylistResult = Assert.IsType<ToolChatMessage>(secondRequest[^1]);
        Assert.Contains("Zin Min", Text(stylistResult));
    }

    [Fact]
    public async Task ReplyAsync_StopsAtConfiguredToolRoundLimit()
    {
        var loopingCall = Completion(
            ChatFinishReason.ToolCalls,
            toolCalls:
            [ChatToolCall.CreateFunctionToolCall("loop", SalonChatTools.ListServicesName, BinaryData.FromString("{}"))]);
        var client = new QueueChatClient(loopingCall, loopingCall);
        using var scope = _factory.Services.CreateScope();
        var agent = CreateAgent(
            client,
            scope.ServiceProvider.GetRequiredService<SalonChatTools>(),
            maxToolRounds: 1);

        var error = await Assert.ThrowsAsync<ChatAgentException>(() => agent.ReplyAsync(
            [new ChatHistoryMessage { Role = "user", Content = "List services." }],
            CancellationToken.None));

        Assert.Equal("The assistant exceeded the tool-call limit.", error.Message);
        Assert.Equal(2, client.Requests.Count);
    }

    private static SalonChatAgent CreateAgent(
        IChatCompletionClient client,
        SalonChatTools tools,
        int maxToolRounds = 5) =>
        new(
            client,
            tools,
            new HuggingFaceOptions { ApiKey = "test", MaxToolRounds = maxToolRounds },
            new SalonOptions(),
            TimeProvider.System);

    private static string Text(ChatMessage message) =>
        string.Concat(message.Content.Select(part => part.Text));

    private static ChatCompletion Completion(
        ChatFinishReason finishReason,
        string content = "",
        IEnumerable<ChatToolCall>? toolCalls = null) =>
        OpenAIChatModelFactory.ChatCompletion(
            id: "completion-id",
            finishReason: finishReason,
            content: [ChatMessageContentPart.CreateTextPart(content)],
            refusal: null,
            toolCalls: toolCalls,
            role: ChatMessageRole.Assistant,
            functionCall: null,
            contentTokenLogProbabilities: null,
            refusalTokenLogProbabilities: null,
            createdAt: DateTimeOffset.UtcNow,
            model: "test-model",
            systemFingerprint: null,
            usage: null);

    private sealed class QueueChatClient(params ChatCompletion[] completions) : IChatCompletionClient
    {
        private readonly Queue<ChatCompletion> _completions = new(completions);

        public List<(IReadOnlyList<ChatMessage> Messages, int ToolCount)> Requests { get; } = [];

        public Task<ChatCompletion> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatCompletionOptions options,
            CancellationToken cancellationToken)
        {
            Requests.Add((messages.ToList(), options.Tools.Count));
            return Task.FromResult(_completions.Dequeue());
        }
    }
}
