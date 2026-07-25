namespace ReelVault.App.Services;

public record SharedContent(string? Url, string? Caption, bool HasValidUrl);

// Platform-agnostic hand-off point for "content arrived via the OS share sheet". Android's
// MainActivity is the only platform-specific publisher today (Platforms/Android), but nothing here
// is Android-specific - the app shell (App.xaml.cs) just reacts to the event regardless of who raised it.
public interface ISharedContentService
{
    event Action<SharedContent>? ContentReceived;

    void Publish(SharedContent content);
}

public class SharedContentService : ISharedContentService
{
    public event Action<SharedContent>? ContentReceived;

    public void Publish(SharedContent content) => ContentReceived?.Invoke(content);
}
