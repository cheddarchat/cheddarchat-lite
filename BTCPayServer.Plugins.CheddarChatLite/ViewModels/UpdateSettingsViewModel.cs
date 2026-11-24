using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.CheddarChatLite.ViewModels;

public class UpdateSettingsViewModel
{
    public string? Id { get; set; }
    public string? StoreId { get; set; }
    public string? StoreDefaultCurrency { get; set; }
    public string? SearchTerm { get; set; }

    // Disabled: the public page and the overlay do not work, settings and URLs are kept
    public bool Archived { get; set; }

    [Required]
    [MaxLength(30)]
    [Display(Name = "Display Title")]
    public string? Title { get; init; }

    [MaxLength(5)]
    [Display(Name = "Currency")]
    public string? Currency { get; set; }

    [Display(Name = "Description")]
    public string? Description { get; init; }

    [Display(Name = "Show the store header")]
    public bool ShowHeader { get; init; }

    [Required]
    [Display(Name = "Button Text")]
    public string? ButtonText { get; init; }

    [Range(0, 2100000000000)]
    [Display(Name = "Minimum amount")]
    public decimal MinAmount { get; init; }

    [Range(1, 500)]
    [Display(Name = "Maximum message length")]
    public int MaxMessageLength { get; init; }

    [Display(Name = "Play alerts automatically")]
    public bool AutoPlay { get; init; }

    [MaxLength(2048)]
    [Display(Name = "Alert image URL")]
    public string? AlertImageUrl { get; init; }

    [Range(1, 60)]
    [Display(Name = "Minimum alert duration (seconds)")]
    public int AlertDuration { get; init; }

    [Display(Name = "Read messages aloud (text-to-speech)")]
    public bool TextToSpeech { get; init; }

    [Display(Name = "Voice")]
    public string? Voice { get; init; }
}
