using System.Security.Cryptography;
using System.Text;
using VeggieAlly.Infrastructure.Line;

namespace VeggieAlly.Application.Tests;

public sealed class LineSignatureValidatorTests
{
    private const string TestSecret = "test-channel-secret-12345";
    private const string TestBodyText = """{"events":[{"type":"message"}]}""";

    private static string ComputeValidSignature(string secret, byte[] body)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(body);
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void Validate_ValidSignature_ReturnsTrue()
    {
        var body = Encoding.UTF8.GetBytes(TestBodyText);
        var signature = ComputeValidSignature(TestSecret, body);

        var result = LineSignatureValidator.Validate(TestSecret, body, signature);

        Assert.True(result);
    }

    [Fact]
    public void Validate_InvalidSignature_ReturnsFalse()
    {
        var body = Encoding.UTF8.GetBytes(TestBodyText);
        var invalidSignature = Convert.ToBase64String(new byte[32]);

        var result = LineSignatureValidator.Validate(TestSecret, body, invalidSignature);

        Assert.False(result);
    }

    [Fact]
    public void Validate_EmptyBody_ReturnsFalse()
    {
        var emptyBody = Array.Empty<byte>();
        var signature = ComputeValidSignature(TestSecret, Encoding.UTF8.GetBytes("anything"));

        var result = LineSignatureValidator.Validate(TestSecret, emptyBody, signature);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmptySecret_ReturnsFalse(string? secret)
    {
        var body = Encoding.UTF8.GetBytes(TestBodyText);

        var result = LineSignatureValidator.Validate(secret!, body, "dummysig==");

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmptySignature_ReturnsFalse(string? signature)
    {
        var body = Encoding.UTF8.GetBytes(TestBodyText);

        var result = LineSignatureValidator.Validate(TestSecret, body, signature!);

        Assert.False(result);
    }
}
