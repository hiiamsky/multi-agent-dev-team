namespace VeggieAlly.Domain.Models.Line;

public sealed record LineEvent(
    string Type,
    string? ReplyToken,
    LineEventSource Source,
    LineMessage? Message);