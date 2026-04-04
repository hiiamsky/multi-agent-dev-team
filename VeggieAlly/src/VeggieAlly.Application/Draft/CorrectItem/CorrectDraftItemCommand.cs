using MediatR;
using VeggieAlly.Domain.Models.Draft;

namespace VeggieAlly.Application.Draft.CorrectItem;

/// <summary>
/// 修正草稿品項價格命令
/// </summary>
public sealed record CorrectDraftItemCommand(
    string TenantId,
    string LineUserId,
    string ItemId,
    decimal? NewBuyPrice,
    decimal? NewSellPrice) : IRequest<DraftItem>;