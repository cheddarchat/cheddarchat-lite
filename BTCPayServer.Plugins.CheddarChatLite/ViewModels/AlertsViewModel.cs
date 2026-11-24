using System.Collections.Generic;
using BTCPayServer.Models;
using BTCPayServer.Plugins.CheddarChatLite.Services;

namespace BTCPayServer.Plugins.CheddarChatLite.ViewModels;

public class AlertsViewModel : BasePagingViewModel
{
    public string AppId { get; init; } = null!;
    public string? OverlayUrl { get; init; }
    public bool AutoPlay { get; init; }
    public int OverlayCount { get; init; }
    public List<Donation> Donations { get; init; } = [];
    public override int CurrentPageCount => Donations.Count;
}
