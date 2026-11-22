namespace ImAI.Application.Abstractions;

// Delivers a freshly issued token without including it in a service response.
public interface ISessionTokenWriter
{
    void Write(string token, DateTime expiresAt);
}
