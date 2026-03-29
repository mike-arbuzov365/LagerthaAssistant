namespace LagerthaAssistant.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class AiSecretProtector : IAiSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[]? _key;
    private readonly ILogger<AiSecretProtector> _logger;

    public AiSecretProtector(
        AiCredentialProtectionOptions options,
        ILogger<AiSecretProtector> logger)
    {
        _logger = logger;
        _key = BuildKey(options.MasterKey);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            throw new InvalidOperationException("API key cannot be empty.");
        }

        if (_key is null)
        {
            throw new InvalidOperationException(
                $"AI credentials encryption key is not configured. " +
                $"Set {AiCredentialProtectionConstants.MasterKeyEnvironmentVariable}.");
        }

        var plainBytes = Encoding.UTF8.GetBytes(plaintext.Trim());
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);

        return $"v1:{Convert.ToBase64String(payload)}";
    }

    public bool TryUnprotect(string ciphertext, out string plaintext)
    {
        plaintext = string.Empty;

        if (_key is null || string.IsNullOrWhiteSpace(ciphertext))
        {
            return false;
        }

        var normalized = ciphertext.Trim();
        if (!normalized.StartsWith("v1:", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var payload = Convert.FromBase64String(normalized["v1:".Length..]);
            if (payload.Length <= NonceSize + TagSize)
            {
                return false;
            }

            var nonce = payload.AsSpan(0, NonceSize).ToArray();
            var tag = payload.AsSpan(NonceSize, TagSize).ToArray();
            var cipher = payload.AsSpan(NonceSize + TagSize).ToArray();
            var plainBytes = new byte[cipher.Length];

            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plainBytes);
            }

            plaintext = Encoding.UTF8.GetString(plainBytes);
            return !string.IsNullOrWhiteSpace(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt AI credential.");
            return false;
        }
    }

    private static byte[]? BuildKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        var trimmed = rawKey.Trim();
        try
        {
            var bytes = Convert.FromBase64String(trimmed);
            if (bytes.Length >= 32)
            {
                return bytes.AsSpan(0, 32).ToArray();
            }
        }
        catch
        {
            // ignore invalid base64 and use hash fallback
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(trimmed));
    }
}
