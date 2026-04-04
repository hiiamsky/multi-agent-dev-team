using MediatR;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.LineEvents.ProcessAudio;

public sealed record ProcessAudioMessageCommand(LineEvent Event) : IRequest;
