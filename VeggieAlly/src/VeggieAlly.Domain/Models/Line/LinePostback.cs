namespace VeggieAlly.Domain.Models.Line;

/// <summary>
/// LINE Postback 事件數據
/// </summary>
public sealed record LinePostback(
    string Data);