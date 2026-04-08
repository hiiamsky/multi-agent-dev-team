using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using VeggieAlly.Infrastructure.Line;

namespace VeggieAlly.Application.Tests;

public sealed class LineTokenServiceTests
{
    private readonly ILogger<LineTokenService> _logger = Substitute.For<ILogger<LineTokenService>>();
    private readonly IOptions<LineOptions> _lineOptions = Options.Create(new LineOptions
    {
        ChannelSecret = "secret",
        ChannelAccessToken = "token",
        ChannelId = "123"
    });

    private static HttpClient CreateMockHttpClient(
        HttpStatusCode verifyStatus, string verifyBody,
        HttpStatusCode profileStatus, string profileBody)
    {
        var handler = new MockHttpMessageHandler(verifyStatus, verifyBody, profileStatus, profileBody);
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.line.me") };
    }

    // --- 11.6 #1 ---
    [Fact]
    public async Task Verify_Valid_ReturnsClaim()
    {
        var client = CreateMockHttpClient(
            HttpStatusCode.OK, """{"scope":"profile","client_id":"123","expires_in":3600}""",
            HttpStatusCode.OK, """{"user_id":"U123","display_name":"TestUser"}""");
        var service = new LineTokenService(client, _logger, _lineOptions);

        var result = await service.VerifyAccessTokenAsync("valid_token");

        result.Should().NotBeNull();
        result!.UserId.Should().Be("U123");
        result.DisplayName.Should().Be("TestUser");
    }

    // --- 11.6 #2 ---
    [Fact]
    public async Task Verify_Expired_ReturnsNull()
    {
        var client = CreateMockHttpClient(
            HttpStatusCode.OK, """{"scope":"profile","client_id":"123","expires_in":0}""",
            HttpStatusCode.OK, """{"user_id":"U123","display_name":"TestUser"}""");
        var service = new LineTokenService(client, _logger, _lineOptions);

        var result = await service.VerifyAccessTokenAsync("expired_token");

        result.Should().BeNull();
    }

    // --- 11.6 #3 ---
    [Fact]
    public async Task Verify_Invalid_ReturnsNull()
    {
        var client = CreateMockHttpClient(
            HttpStatusCode.BadRequest, """{"error":"invalid_request"}""",
            HttpStatusCode.OK, "");
        var service = new LineTokenService(client, _logger, _lineOptions);

        var result = await service.VerifyAccessTokenAsync("invalid_token");

        result.Should().BeNull();
    }

    // --- 11.6 #4 ---
    [Fact]
    public async Task Verify_HttpTimeout_ReturnsNull()
    {
        var handler = new TimeoutHttpMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.line.me") };
        var service = new LineTokenService(client, _logger, _lineOptions);

        var result = await service.VerifyAccessTokenAsync("some_token");

        result.Should().BeNull();
    }

    /// <summary>
    /// 模擬 LINE API 的 HttpMessageHandler
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _verifyStatus;
        private readonly string _verifyBody;
        private readonly HttpStatusCode _profileStatus;
        private readonly string _profileBody;
        private int _callCount;

        public MockHttpMessageHandler(
            HttpStatusCode verifyStatus, string verifyBody,
            HttpStatusCode profileStatus, string profileBody)
        {
            _verifyStatus = verifyStatus;
            _verifyBody = verifyBody;
            _profileStatus = profileStatus;
            _profileBody = profileBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _callCount++;

            if (request.RequestUri?.PathAndQuery.Contains("/oauth2/v2.1/verify") == true)
            {
                return Task.FromResult(new HttpResponseMessage(_verifyStatus)
                {
                    Content = new StringContent(_verifyBody, System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (request.RequestUri?.PathAndQuery.Contains("/v2/profile") == true)
            {
                return Task.FromResult(new HttpResponseMessage(_profileStatus)
                {
                    Content = new StringContent(_profileBody, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("Request timed out", new TimeoutException());
        }
    }
}
