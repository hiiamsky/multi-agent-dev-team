using MediatR;

namespace VeggieAlly.Application.Menu.Unpublish;

/// <summary>
/// 撤回發布命令 — MVP 階段回傳 501 Not Implemented
/// </summary>
public sealed record UnpublishMenuCommand(
    string TenantId) : IRequest;