namespace SharedBotKernel.Tests.Infrastructure.AI;

using Microsoft.Extensions.Logging.Abstractions;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Options;
using Xunit;

public sealed class AiSecretProtectorTests
{
    [Fact]
    public void Protect_ShouldThrowInvalidOperationException_WhenPlaintextIsWhitespace()
    {
        var sut = CreateSut(GenerateBase64Key(1));

        Assert.Throws<InvalidOperationException>(() => sut.Protect("   "));
    }

    [Fact]
    public void Protect_ShouldThrowInvalidOperationException_WhenMasterKeyMissing()
    {
        var sut = CreateSut(masterKey: null);

        Assert.Throws<InvalidOperationException>(() => sut.Protect("sk-test"));
    }

    [Fact]
    public void Protect_ShouldReturnVersionedCiphertext_WhenInputValid()
    {
        var sut = CreateSut(GenerateBase64Key(2));

        var ciphertext = sut.Protect("sk-test");

        Assert.StartsWith("v1:", ciphertext, StringComparison.Ordinal);
        Assert.NotEqual("sk-test", ciphertext);
    }

    [Fact]
    public void TryUnprotect_ShouldReturnOriginalPlaintext_WhenCiphertextCreatedWithSameKey()
    {
        var sut = CreateSut(GenerateBase64Key(3));
        var ciphertext = sut.Protect("  sk-roundtrip  ");

        var success = sut.TryUnprotect(ciphertext, out var plaintext);

        Assert.True(success);
        Assert.Equal("sk-roundtrip", plaintext);
    }

    [Fact]
    public void TryUnprotect_ShouldReturnFalse_WhenCiphertextMalformed()
    {
        var sut = CreateSut(GenerateBase64Key(4));

        var success = sut.TryUnprotect("v1:not-base64!", out var plaintext);

        Assert.False(success);
        Assert.Equal(string.Empty, plaintext);
    }

    [Fact]
    public void TryUnprotect_ShouldReturnFalse_WhenCiphertextEncryptedWithDifferentKey()
    {
        var producer = CreateSut(GenerateBase64Key(5));
        var consumer = CreateSut(GenerateBase64Key(6));
        var ciphertext = producer.Protect("sk-test");

        var success = consumer.TryUnprotect(ciphertext, out var plaintext);

        Assert.False(success);
        Assert.Equal(string.Empty, plaintext);
    }

    private static AiSecretProtector CreateSut(string? masterKey)
    {
        return new AiSecretProtector(
            new AiCredentialProtectionOptions
            {
                MasterKey = masterKey
            },
            NullLogger<AiSecretProtector>.Instance);
    }

    private static string GenerateBase64Key(byte seed)
    {
        var bytes = Enumerable.Range(0, 32)
            .Select(index => (byte)(seed + index))
            .ToArray();

        return Convert.ToBase64String(bytes);
    }
}
