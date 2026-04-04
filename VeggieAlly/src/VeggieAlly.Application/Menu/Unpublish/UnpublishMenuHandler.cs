using MediatR;

namespace VeggieAlly.Application.Menu.Unpublish;

/// <summary>
/// 撤回發布處理器 — MVP 命令回傳 NotImplementedException
/// </summary>
public sealed class UnpublishMenuHandler : IRequestHandler<UnpublishMenuCommand>
{
    public Task Handle(UnpublishMenuCommand request, CancellationToken cancellationToken)
    {
        // MVP 階段不實作撤回功能，出於安全和資料一致性考量
        throw new NotImplementedException("撤回功能将在後續版本實作");
    }
}