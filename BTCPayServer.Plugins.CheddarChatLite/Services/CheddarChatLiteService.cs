using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.CheddarChatLite.Services;

public record Donation(
    string InvoiceId,
    string? Name,
    string Message,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp,
    DateTimeOffset? PlayedAt);

public class CheddarChatLiteService(
    EventAggregator eventAggregator,
    InvoiceRepository invoiceRepository,
    DisplayFormatter displayFormatter)
{
    // Invoice metadata keys
    public const string MetadataKey = "cheddarChat";
    public const string PlayedAtKey = "cheddarChatPlayedAt";

    private const string SkipMessage = """{"type":"skip"}""";
    private const string RefreshMessage = "refresh";
    private const string PingMessage = "ping";
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, int> _overlayConnections = new();

    public static string CreateOverlayKey() => Encoders.Base58.EncodeData(RandomUtils.GetBytes(24));

    public static bool IsOverlayKeyValid(CheddarChatLiteSettings settings, string? key) =>
        !string.IsNullOrEmpty(settings.OverlayKey) && !string.IsNullOrEmpty(key) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(settings.OverlayKey), Encoding.UTF8.GetBytes(key));

    public static JObject CreateMetadata(string? name, string message) => new()
    {
        ["name"] = name,
        ["message"] = message
    };

    public static Donation? GetDonation(InvoiceEntity invoice)
    {
        if (invoice.Metadata.AdditionalData?.TryGetValue(MetadataKey, out var token) is not true ||
            token is not JObject data ||
            data["message"]?.ToString() is not { Length: > 0 } message)
            return null;

        var name = data["name"]?.ToString();
        var playedAt = invoice.Metadata.GetAdditionalData<long?>(PlayedAtKey);
        return new Donation(
            invoice.Id,
            string.IsNullOrWhiteSpace(name) ? null : name,
            message,
            invoice.PaidAmount.Net > 0 ? invoice.PaidAmount.Net : invoice.Price,
            invoice.Currency,
            invoice.InvoiceTime,
            playedAt is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null);
    }

    public int GetOverlayCount(string appId) => _overlayConnections.TryGetValue(appId, out var count) ? count : 0;

    /// <summary>
    /// Called for every settled invoice of the app: Updates the dashboards and plays the alert in auto mode.
    /// </summary>
    public async Task HandleSettledInvoice(AppData app, InvoiceEntity invoice)
    {
        if (GetDonation(invoice) is not { } donation)
            return;

        var autoPlay = app.GetSettings<CheddarChatLiteSettings>().AutoPlay && donation.PlayedAt is null;
        // PlayAlert refreshes the dashboards itself once the alert has been played
        if (!autoPlay || !await PlayAlert(app, donation))
            RefreshDashboards(app.Id);
    }

    /// <summary>
    /// Sends the alert to the connected overlays and marks the donation as played.
    /// </summary>
    /// <returns>false if no overlay is connected, in which case the donation stays unplayed</returns>
    public async Task<bool> PlayAlert(AppData app, Donation donation)
    {
        if (GetOverlayCount(app.Id) == 0)
            return false;

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        Publish(app.Id, CheddarChatLiteEventType.Alert,
            CreateAlert(settings, donation.InvoiceId, donation.Name, donation.Amount, donation.Currency, donation.Message));
        await invoiceRepository.UpdateInvoiceMetadata(donation.InvoiceId, PlayedAtKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        RefreshDashboards(app.Id);
        return true;
    }

    public bool PlayTestAlert(AppData app)
    {
        if (GetOverlayCount(app.Id) == 0)
            return false;

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        var amount = Math.Max(settings.MinAmount, 5);
        Publish(app.Id, CheddarChatLiteEventType.Alert,
            CreateAlert(settings, null, "CheddarChat", amount, settings.Currency ?? "USD", "This is a test alert. Thanks for the cheddar!"));
        return true;
    }

    public bool SkipAlert(string appId)
    {
        Publish(appId, CheddarChatLiteEventType.Skip);
        return GetOverlayCount(appId) > 0;
    }

    public void DisconnectOverlays(string appId) => Publish(appId, CheddarChatLiteEventType.DisconnectOverlays);

    public void RefreshDashboards(string appId) => Publish(appId, CheddarChatLiteEventType.Refresh);

    public Task RunOverlaySocket(string appId, WebSocket socket, CancellationToken cancellationToken) =>
        RunSocket(appId, socket, true, cancellationToken);

    public Task RunDashboardSocket(string appId, WebSocket socket, CancellationToken cancellationToken) =>
        RunSocket(appId, socket, false, cancellationToken);

    private string CreateAlert(CheddarChatLiteSettings settings, string? id, string? name, decimal amount, string currency, string message) =>
        new JObject
        {
            ["type"] = "alert",
            ["id"] = id,
            ["name"] = name ?? "Anonymous",
            ["amount"] = displayFormatter.Currency(amount, currency, DisplayFormatter.CurrencyFormat.Symbol),
            ["message"] = message,
            ["speech"] = CheddarChatLiteSpeech.GetSpeech(message),
            ["image"] = settings.AlertImageUrl,
            ["tts"] = settings.TextToSpeech,
            ["voice"] = CheddarChatLiteVoices.IsValid(settings.Voice) ? settings.Voice : null,
            ["duration"] = settings.AlertDuration
        }.ToString(Formatting.None);

    private void Publish(string appId, CheddarChatLiteEventType type, string? payload = null) =>
        eventAggregator.Publish(new CheddarChatLiteEvent(appId, type, payload));

    private void ChangeOverlayCount(string appId, int delta)
    {
        var count = _overlayConnections.AddOrUpdate(appId, Math.Max(0, delta), (_, current) => Math.Max(0, current + delta));
        // Don't keep an entry for every app that ever had an overlay. Only removed if it is still zero.
        if (count == 0)
            _overlayConnections.TryRemove(new KeyValuePair<string, int>(appId, 0));
        RefreshDashboards(appId);
    }

    private async Task RunSocket(string appId, WebSocket socket, bool isOverlay, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // A WebSocket supports only one send operation at a time
        var sendLock = new SemaphoreSlim(1, 1);
        var subscription = eventAggregator.SubscribeAsync<CheddarChatLiteEvent>(async evt =>
        {
            if (evt.AppId != appId)
                return;
            if (isOverlay && evt.Type == CheddarChatLiteEventType.DisconnectOverlays)
            {
                await cts.CancelAsync();
                return;
            }
            var message = (isOverlay, evt.Type) switch
            {
                (true, CheddarChatLiteEventType.Alert) => evt.Payload,
                (true, CheddarChatLiteEventType.Skip) => SkipMessage,
                (false, CheddarChatLiteEventType.Refresh) => RefreshMessage,
                _ => null
            };
            if (message is null)
                return;
            try
            {
                await Send(socket, sendLock, message, cts.Token);
            }
            catch
            {
                // The connection is gone, the receive loop below cleans up
            }
        });

        if (isOverlay)
            ChangeOverlayCount(appId, 1);
        try
        {
            // Clients only listen, so a single receive stays pending for the whole connection. It completes when
            // the client closes the connection, or sends a message, which the protocol does not allow either.
            var receiving = socket.ReceiveAsync(new ArraySegment<byte>(new byte[1]), cts.Token);
            var ping = Task.Delay(PingInterval, cts.Token);
            // Ping regularly, so that proxies keep the connection open and clients can detect stale connections.
            // Like BTCPay Server's ReceiveAndPingAsync, but its ping would bypass the send lock.
            while (await Task.WhenAny(receiving, ping) == ping)
            {
                await ping;
                await Send(socket, sendLock, PingMessage, cts.Token);
                ping = Task.Delay(PingInterval, cts.Token);
            }
            await receiving;
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
        }
        finally
        {
            subscription.Dispose();
            if (isOverlay)
                ChangeOverlayCount(appId, -1);
            await socket.CloseSocket();
        }
    }

    private static async Task Send(WebSocket socket, SemaphoreSlim sendLock, string message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        await sendLock.WaitAsync(timeout.Token);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, timeout.Token);
        }
        finally
        {
            sendLock.Release();
        }
    }
}
