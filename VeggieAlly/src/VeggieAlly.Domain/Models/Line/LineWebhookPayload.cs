namespace VeggieAlly.Domain.Models.Line;

public sealed record LineWebhookPayload(List<LineEvent> Events);