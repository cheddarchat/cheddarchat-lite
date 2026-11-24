using BTCPayServer.Plugins.CheddarChatLite.Services;

namespace BTCPayServer.Plugins.CheddarChatLite;

public class CheddarChatLiteSettings
{
    public string? Title { get; init; }
    public string? Currency { get; init; }
    public string? Description { get; init; }
    public bool ShowHeader { get; init; } = true;
    public string? ButtonText { get; init; } = "Send CheddarChat";
    public decimal MinAmount { get; init; }
    public int MaxMessageLength { get; init; } = 200;

    // Alerts
    public bool AutoPlay { get; set; } = true;
    public string? AlertImageUrl { get; init; }
    public int AlertDuration { get; init; } = 8;
    public bool TextToSpeech { get; init; } = true;
    // A voice of CheddarChatLiteVoices, or empty for the voice that is built into the browser
    public string? Voice { get; init; } = CheddarChatLiteVoices.Default;

    // Secret part of the OBS overlay URL
    public string? OverlayKey { get; set; }
}
