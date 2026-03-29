namespace SharedBotKernel.Infrastructure.AI;

public interface IAiSecretProtector
{
    string Protect(string plaintext);

    bool TryUnprotect(string ciphertext, out string plaintext);
}
