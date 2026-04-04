using MediatR;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Draft;

namespace VeggieAlly.Application.Draft.CorrectItem;

/// <summary>
/// 修正草稿品項價格處理器
/// </summary>
public sealed class CorrectDraftItemHandler : IRequestHandler<CorrectDraftItemCommand, DraftItem>
{
    private readonly IDraftMenuService _draftMenuService;

    public CorrectDraftItemHandler(IDraftMenuService draftMenuService)
    {
        _draftMenuService = draftMenuService;
    }

    public async Task<DraftItem> Handle(CorrectDraftItemCommand request, CancellationToken cancellationToken)
    {
        return await _draftMenuService.CorrectItemPriceAsync(
            request.TenantId,
            request.LineUserId,
            request.ItemId,
            request.NewBuyPrice,
            request.NewSellPrice,
            cancellationToken);
    }
}