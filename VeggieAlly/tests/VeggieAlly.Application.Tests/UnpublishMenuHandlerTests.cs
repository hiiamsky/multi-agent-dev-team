using VeggieAlly.Application.Menu.Unpublish;

namespace VeggieAlly.Application.Tests;

public sealed class UnpublishMenuHandlerTests
{
    [Fact]
    public async Task Unpublish_ThrowsNotImplemented()
    {
        var handler = new UnpublishMenuHandler();
        var command = new UnpublishMenuCommand("tenant-1");

        await Assert.ThrowsAsync<NotImplementedException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}
