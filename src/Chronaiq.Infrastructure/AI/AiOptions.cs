namespace Chronaiq.Infrastructure.AI;

/// <summary>Which chat/embedding backend the AI services should target.</summary>
public enum AiProvider
{
    /// <summary>No external model configured — deterministic local behavior is used.</summary>
    None = 0,
    OpenAI = 1,
    AzureOpenAI = 2,

    /// <summary>Anthropic Claude via the official SDK (chat/coordinator path).</summary>
    Anthropic = 3
}

/// <summary>
/// Binds the <c>Ai</c> configuration section. When <see cref="Provider"/> is
/// <see cref="AiProvider.None"/> (the default, and whenever <see cref="ApiKey"/> is blank) the
/// system runs fully offline: chat responses are produced by a deterministic narrator and
/// embeddings by a stable local hash. Supplying a key promotes the Semantic Kernel chat path.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.None;

    public string? ApiKey { get; set; }

    /// <summary>Required for <see cref="AiProvider.AzureOpenAI"/>.</summary>
    public string? Endpoint { get; set; }

    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>Claude model id used when <see cref="Provider"/> is <see cref="AiProvider.Anthropic"/>.</summary>
    public string AnthropicModel { get; set; } = "claude-opus-5";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Must match the <c>vector(1536)</c> column width.</summary>
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>True when a usable chat model is configured for any provider.</summary>
    public bool IsChatEnabled => Provider != AiProvider.None && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// True when the configured chat provider is Anthropic Claude. Does not require
    /// <see cref="ApiKey"/> — when it is blank the official SDK resolves credentials from the
    /// environment (<c>ANTHROPIC_API_KEY</c>, auth profiles, etc.).
    /// </summary>
    public bool IsAnthropic => Provider == AiProvider.Anthropic;
}
