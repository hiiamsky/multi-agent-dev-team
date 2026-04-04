namespace VeggieAlly.Domain.Exceptions;

/// <summary>
/// 菜單已發布異常
/// </summary>
public sealed class MenuAlreadyPublishedException : Exception
{
    public MenuAlreadyPublishedException()
        : base("今日菜單已發布")
    {
    }
}