using MediatR;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.GetToday;

/// <summary>
/// 今日菜單查詢
/// </summary>
public sealed record GetTodayMenuQuery(
    string TenantId) : IRequest<PublishedMenu?>;