namespace VeggieAlly.Infrastructure.AI;

public sealed class GeminiOptions
{
    public required string ApiKey { get; init; }
    public string ModelId { get; init; } = "gemini-2.0-flash";
}