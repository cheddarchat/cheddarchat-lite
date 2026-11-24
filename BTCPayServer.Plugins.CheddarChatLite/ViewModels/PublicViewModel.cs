using System.ComponentModel.DataAnnotations;
using BTCPayServer.Models;

namespace BTCPayServer.Plugins.CheddarChatLite.ViewModels;

public class PublicViewModel
{
    public string? AppId { get; init; }
    public string? StoreName { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Currency { get; init; }
    public string? ButtonText { get; init; }
    public bool ShowHeader { get; init; }
    public decimal MinAmount { get; init; }
    public int MaxMessageLength { get; init; }
    public StoreBrandingViewModel? StoreBranding { get; init; }
    public DonationViewModel Donation { get; init; } = null!;
}

public class DonationViewModel
{
    [MaxLength(50)]
    [Display(Name = "Name")]
    public string? Name { get; init; }

    [Required]
    [Display(Name = "Message")]
    public string? Message { get; init; }

    // Has to be more than zero, which the controller checks: the smallest amount depends on the currency
    [Range(0, 2100000000000)]
    [Display(Name = "Amount")]
    public decimal Amount { get; init; }
}
