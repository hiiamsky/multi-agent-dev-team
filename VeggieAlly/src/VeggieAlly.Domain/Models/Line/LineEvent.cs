namespace VeggieAlly.Domain.Models.Line;

/// <summary>
/// LINE 事件 — 支援 message 和 postback 事件
/// </summary>
public sealed record LineEvent(
    string Type,
    string? ReplyToken,
    LineEventSource Source,
    LineMessage? Message,
    LinePostback? Postback = null);