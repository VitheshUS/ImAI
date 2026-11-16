namespace ImAI.Application.Abstractions;

public interface IChatResponder
{
    Task<string> RespondAsync(string query, CancellationToken cancellationToken);
}
