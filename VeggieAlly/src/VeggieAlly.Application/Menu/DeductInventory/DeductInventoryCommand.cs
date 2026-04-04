using MediatR;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.DeductInventory;

/// <summary>
/// 庫存扣除命令
/// </summary>
public sealed record DeductInventoryCommand(
    string TenantId,
    string ItemId,
    int Amount) : IRequest<PublishedMenuItem>;