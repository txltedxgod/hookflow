using HookFlow.Services;
using Xunit;

namespace HookFlow.Tests;

public class SignatureServiceTests
{
    private readonly SignatureService _signer = new();

    [Fact]
    public void ComputeHmacSha256_ValidPayloadAndSecret_ReturnsConsistentHexHash()
    {
        // Arrange
        var payload = "{\"event\":\"order.created\",\"data\":{\"id\":42}}";
        var secret = "test_secret_key_12345";

        // Act
        var signature1 = _signer.ComputeHmacSha256(payload, secret);
        var signature2 = _signer.ComputeHmacSha256(payload, secret);

        // Assert
        Assert.NotNull(signature1);
        Assert.Equal(64, signature1.Length); // 256 bits = 64 hex characters
        Assert.Equal(signature1, signature2);
    }

    [Fact]
    public void ComputeHmacSha256_DifferentPayload_ProducesDifferentSignature()
    {
        // Arrange
        var payloadA = "{\"event\":\"user.signup\"}";
        var payloadB = "{\"event\":\"user.deleted\"}";
        var secret = "my_app_secret";

        // Act
        var sigA = _signer.ComputeHmacSha256(payloadA, secret);
        var sigB = _signer.ComputeHmacSha256(payloadB, secret);

        // Assert
        Assert.NotEqual(sigA, sigB);
    }
}
