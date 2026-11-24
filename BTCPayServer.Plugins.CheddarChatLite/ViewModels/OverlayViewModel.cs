namespace BTCPayServer.Plugins.CheddarChatLite.ViewModels;

public class OverlayViewModel
{
    public string? Title { get; init; }
    public string SocketUrl { get; init; } = null!;
    public string? ImageUrl { get; init; }
    public string AccentColor { get; init; } = null!;
    public string? Voice { get; init; }
}
