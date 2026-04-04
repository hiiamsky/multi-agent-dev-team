using MediatR;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.LineEvents.ProcessText;

public sealed record ProcessTextMessageCommand(LineEvent Event) : IRequest;