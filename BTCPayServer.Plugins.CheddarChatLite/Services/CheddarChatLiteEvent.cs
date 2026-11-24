namespace BTCPayServer.Plugins.CheddarChatLite.Services;

public enum CheddarChatLiteEventType
{
    /// <summary>Play an alert in the overlays, the payload contains the alert JSON</summary>
    Alert,
    /// <summary>Stop the alert that is currently playing in the overlays</summary>
    Skip,
    /// <summary>Close the overlay connections, e.g. because the overlay URL was reset</summary>
    DisconnectOverlays,
    /// <summary>Tell the alerts dashboards that something changed</summary>
    Refresh
}

public record CheddarChatLiteEvent(string AppId, CheddarChatLiteEventType Type, string? Payload = null);
