using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace XafGitHubCopilot.Module.Services
{
    /// <summary>
    /// Adapter that wraps <see cref="CopilotChatService"/> as an
    /// <see cref="IChatClient"/> so the DevExpress AI infrastructure
    /// (<c>DxAIChat</c>, <c>AIChatControl</c>) can route messages
    /// through the GitHub Copilot SDK automatically.
    /// </summary>
    public sealed class CopilotChatClient : IChatClient
    {
        private readonly CopilotChatService _service;

        public CopilotChatClient(CopilotChatService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ChatClientMetadata Metadata => new("CopilotChat");

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var lastUserMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
            var prompt = lastUserMessage?.Text ?? string.Empty;

            var response = await _service.AskAsync(prompt, cancellationToken);

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastUserMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
            var prompt = lastUserMessage?.Text ?? string.Empty;

            await foreach (var chunk in _service.AskStreamingAsync(prompt, cancellationToken).ConfigureAwait(false))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(chunk)]
                };
            }
        }

        public object GetService(Type serviceType, object serviceKey = null)
            => serviceType == typeof(CopilotChatClient) ? this : null;

        public void Dispose() { }
    }
}
