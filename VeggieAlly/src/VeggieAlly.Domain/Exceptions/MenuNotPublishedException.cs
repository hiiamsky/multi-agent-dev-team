namespace VeggieAlly.Domain.Exceptions;

/// <summary>
/// 尚未發布菜單異常
/// </summary>
public sealed class MenuNotPublishedException : Exception
{
    public MenuNotPublishedException()
        : base("尚未發布菜單")
    {
    }
}