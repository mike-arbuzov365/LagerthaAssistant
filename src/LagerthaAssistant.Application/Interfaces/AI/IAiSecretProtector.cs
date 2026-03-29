namespace LagerthaAssistant.Application.Interfaces.AI;

public interface IAiSecretProtector
{
    string Protect(string plaintext);

    bool TryUnprotect(string ciphertext, out string plaintext);
}
