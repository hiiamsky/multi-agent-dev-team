using MediatR;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.Publish;

public sealed record PublishMenuCommand(string TenantId, string LineUserId) : IRequest<PublishedMenu>;
