using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Filters;
using BTCPayServer.Models;
using BTCPayServer.Plugins.CheddarChatLite.Services;
using BTCPayServer.Plugins.CheddarChatLite.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NicolasDorier.RateLimits;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.CheddarChatLite.Controllers;

[Route("apps")]
[AutoValidateAntiforgeryToken]
public class UICheddarChatLiteController(
    AppService appService,
    ApplicationDbContextFactory dbContextFactory,
    UriResolver uriResolver,
    CurrencyNameTable currencies,
    DisplayFormatter displayFormatter,
    InvoiceRepository invoiceRepository,
    UIInvoiceController invoiceController,
    CheddarChatLiteService cheddarChatService,
    IStringLocalizer stringLocalizer)
    : Controller
{
    private const string DefaultAccentColor = "#F5A623";
    // Name of the app in BTCPay Server's own lists, e.g. on the store dashboard
    private const string AppName = "CheddarChat Lite";
    public IStringLocalizer StringLocalizer { get; } = stringLocalizer;

    // A store has one CheddarChat. Until it gets enabled, the navigation link leads to the "Enable" page.
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpGet("/stores/{storeId}/cheddarchat")]
    public async Task<IActionResult> Index(string storeId)
    {
        var app = await GetStoreApp(storeId);
        return app == null ? View("Enable") : RedirectToAction(nameof(UpdateSettings), new { appId = app.Id });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("/stores/{storeId}/cheddarchat")]
    public async Task<IActionResult> Enable(string storeId)
    {
        var store = GetCurrentStore();
        if (store == null)
            return NotFound();

        var app = await GetStoreApp(store.Id);
        if (app == null)
        {
            app = new AppData { StoreDataId = store.Id, Name = AppName, AppType = CheddarChatLiteApp.AppType };
            await appService.SetDefaultSettings(app, store.GetStoreBlob().DefaultCurrency);
            await appService.UpdateOrCreateApp(app);
        }
        else if (app.Archived)
        {
            await appService.SetArchived(app, false);
        }
        TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["CheddarChat Lite has been enabled"].Value;
        return RedirectToAction(nameof(UpdateSettings), new { appId = app.Id });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/disable")]
    public async Task<IActionResult> Disable(string appId)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        if (!app.Archived)
        {
            await appService.SetArchived(app, true);
            cheddarChatService.DisconnectOverlays(app.Id);
        }
        TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["CheddarChat Lite has been disabled"].Value;
        return RedirectToAction(nameof(UpdateSettings), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpGet("{appId}/settings/cheddarchat")]
    public IActionResult UpdateSettings(string appId)
    {
        var app = GetCurrentApp();
        var store = GetCurrentStore();
        if (app == null || store == null)
            return NotFound();

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        var vm = new UpdateSettingsViewModel
        {
            Id = appId,
            Archived = app.Archived,
            StoreId = app.StoreDataId,
            StoreDefaultCurrency = store.GetStoreBlob().DefaultCurrency,
            SearchTerm = GetSearchTerm(app),
            Title = GetTitle(settings, store),
            Currency = settings.Currency,
            Description = settings.Description,
            ShowHeader = settings.ShowHeader,
            ButtonText = settings.ButtonText,
            MinAmount = settings.MinAmount,
            MaxMessageLength = settings.MaxMessageLength,
            AutoPlay = settings.AutoPlay,
            AlertImageUrl = settings.AlertImageUrl,
            AlertDuration = settings.AlertDuration,
            TextToSpeech = settings.TextToSpeech,
            Voice = settings.Voice
        };
        return View(vm);
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat")]
    public async Task<IActionResult> UpdateSettings(string appId, UpdateSettingsViewModel vm)
    {
        var app = GetCurrentApp();
        var store = GetCurrentStore();
        if (app == null || store == null)
            return NotFound();

        var storeBlob = store.GetStoreBlob();
        vm.Currency = GetStoreDefaultCurrencyIfEmpty(storeBlob, vm.Currency);
        if (currencies.GetCurrencyData(vm.Currency, false) == null)
            ModelState.AddModelError(nameof(vm.Currency), StringLocalizer["Invalid currency"]);
        if (!string.IsNullOrEmpty(vm.AlertImageUrl) && !IsHttpUrl(vm.AlertImageUrl))
            ModelState.AddModelError(nameof(vm.AlertImageUrl), StringLocalizer["Please enter a valid http(s) URL"]);
        if (!string.IsNullOrEmpty(vm.Voice) && !CheddarChatLiteVoices.IsValid(vm.Voice))
            ModelState.AddModelError(nameof(vm.Voice), StringLocalizer["Please select a voice"]);

        if (!ModelState.IsValid)
        {
            vm.Id = appId;
            vm.StoreId = app.StoreDataId;
            vm.Archived = app.Archived;
            vm.StoreDefaultCurrency = storeBlob.DefaultCurrency;
            vm.SearchTerm = GetSearchTerm(app);
            return View(vm);
        }

        var current = app.GetSettings<CheddarChatLiteSettings>();
        var settings = new CheddarChatLiteSettings
        {
            Title = vm.Title,
            Currency = vm.Currency,
            Description = vm.Description,
            ShowHeader = vm.ShowHeader,
            ButtonText = vm.ButtonText,
            MinAmount = vm.MinAmount,
            MaxMessageLength = vm.MaxMessageLength,
            AutoPlay = vm.AutoPlay,
            AlertImageUrl = string.IsNullOrWhiteSpace(vm.AlertImageUrl) ? null : vm.AlertImageUrl.Trim(),
            AlertDuration = vm.AlertDuration,
            TextToSpeech = vm.TextToSpeech,
            Voice = vm.Voice ?? string.Empty,
            OverlayKey = current.OverlayKey ?? CheddarChatLiteService.CreateOverlayKey()
        };

        app.SetSettings(settings);
        await appService.UpdateOrCreateApp(app);
        cheddarChatService.RefreshDashboards(app.Id);
        TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["The settings have been updated"].Value;
        return RedirectToAction(nameof(UpdateSettings), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpGet("{appId}/settings/cheddarchat/alerts")]
    public async Task<IActionResult> Alerts(string appId, int skip = 0, int count = BasePagingViewModel.CountDefault)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        if (string.IsNullOrEmpty(settings.OverlayKey))
        {
            settings.OverlayKey = CheddarChatLiteService.CreateOverlayKey();
            app.SetSettings(settings);
            await appService.UpdateOrCreateApp(app);
        }

        var invoices = await invoiceRepository.GetInvoices(new InvoiceQuery
        {
            StoreId = [app.StoreDataId],
            TextSearch = AppService.GetAppSearchTerm(app),
            Status = [InvoiceStatus.Settled.ToString()],
            IncludeArchived = true,
            Skip = skip,
            Take = count
        });

        var vm = new AlertsViewModel
        {
            AppId = app.Id,
            AutoPlay = settings.AutoPlay,
            OverlayCount = cheddarChatService.GetOverlayCount(app.Id),
            OverlayUrl = Url.Action(nameof(Overlay), "UICheddarChatLite", new { appId = app.Id, key = settings.OverlayKey }, Request.Scheme),
            Donations = invoices.Select(CheddarChatLiteService.GetDonation).OfType<Donation>().ToList(),
            Skip = skip,
            Count = count
        };
        // The Alerts page script only needs the live-updating part, not the whole layout
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView("_AlertsList", vm) : View(vm);
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/alerts/{invoiceId}/play")]
    public async Task<IActionResult> PlayAlert(string appId, string invoiceId, int? skip, int? count)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        var invoice = await invoiceRepository.GetInvoice(invoiceId);
        if (invoice == null ||
            invoice.StoreId != app.StoreDataId ||
            invoice.Status != InvoiceStatus.Settled ||
            !AppService.GetAppInternalTags(invoice).Contains(app.Id) ||
            CheddarChatLiteService.GetDonation(invoice) is not { } donation)
            return NotFound();

        if (await cheddarChatService.PlayAlert(app, donation))
            TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["The alert has been sent to the overlay"].Value;
        else
            SetNoOverlayError();
        return RedirectToAction(nameof(Alerts), new { appId, skip, count });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/alerts/test")]
    public IActionResult TestAlert(string appId)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        if (cheddarChatService.PlayTestAlert(app))
            TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["A test alert has been sent to the overlay"].Value;
        else
            SetNoOverlayError();
        return RedirectToAction(nameof(Alerts), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/alerts/skip")]
    public IActionResult SkipAlert(string appId)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        if (cheddarChatService.SkipAlert(app.Id))
            TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["The current alert has been skipped"].Value;
        else
            SetNoOverlayError();
        return RedirectToAction(nameof(Alerts), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/alerts/autoplay")]
    public async Task<IActionResult> SetAutoPlay(string appId, bool autoPlay)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        settings.AutoPlay = autoPlay;
        app.SetSettings(settings);
        await appService.UpdateOrCreateApp(app);
        cheddarChatService.RefreshDashboards(app.Id);
        TempData[WellKnownTempData.SuccessMessage] = autoPlay
            ? StringLocalizer["New CheddarChats will play automatically"].Value
            : StringLocalizer["New CheddarChats need to be played manually"].Value;
        return RedirectToAction(nameof(Alerts), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpPost("{appId}/settings/cheddarchat/alerts/overlay-url")]
    public async Task<IActionResult> ResetOverlayUrl(string appId)
    {
        var app = GetCurrentApp();
        if (app == null)
            return NotFound();

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        settings.OverlayKey = CheddarChatLiteService.CreateOverlayKey();
        app.SetSettings(settings);
        await appService.UpdateOrCreateApp(app);
        cheddarChatService.DisconnectOverlays(app.Id);
        TempData[WellKnownTempData.SuccessMessage] = StringLocalizer["The overlay URL has been reset. Update the browser source in OBS with the new URL."].Value;
        return RedirectToAction(nameof(Alerts), new { appId });
    }

    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [HttpGet("{appId}/settings/cheddarchat/alerts/ws")]
    public async Task<IActionResult> AlertsSocket(string appId, CancellationToken cancellationToken)
    {
        var app = GetCurrentApp();
        if (app == null || !HttpContext.WebSockets.IsWebSocketRequest)
            return NotFound();

        var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await cheddarChatService.RunDashboardSocket(app.Id, socket, cancellationToken);
        return new EmptyResult();
    }

    [HttpGet("/")]
    [HttpGet("/apps/{appId}/cheddarchat")]
    [DomainMappingConstraint(CheddarChatLiteApp.AppType)]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    public async Task<IActionResult> Public(string appId)
    {
        var app = await appService.GetApp(appId, CheddarChatLiteApp.AppType, true);
        if (app == null)
            return NotFound();

        return View(await GetPublicViewModel(app, new DonationViewModel()));
    }

    [HttpPost("/")]
    [HttpPost("/apps/{appId}/cheddarchat")]
    [IgnoreAntiforgeryToken]
    [EnableCors(CorsPolicies.All)]
    [DomainMappingConstraint(CheddarChatLiteApp.AppType)]
    [RateLimitsFilter(ZoneLimits.PublicInvoices, Scope = RateLimitsScope.RemoteAddress)]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    public async Task<IActionResult> Public(string appId, DonationViewModel? donation)
    {
        var app = await appService.GetApp(appId, CheddarChatLiteApp.AppType, true);
        if (app == null)
            return NotFound();

        donation ??= new DonationViewModel();
        var settings = app.GetSettings<CheddarChatLiteSettings>();
        var name = donation.Name?.Trim();
        // Browsers count line breaks as one character for maxlength, but submit them as two
        var message = donation.Message?.Replace("\r\n", "\n").Trim();
        // An invoice without an amount would be settled right away: a free alert
        if (donation.Amount <= 0)
            ModelState.AddModelError($"{nameof(PublicViewModel.Donation)}.{nameof(DonationViewModel.Amount)}",
                StringLocalizer["Please enter an amount"]);
        else if (settings.MinAmount > 0 && donation.Amount < settings.MinAmount)
            ModelState.AddModelError($"{nameof(PublicViewModel.Donation)}.{nameof(DonationViewModel.Amount)}",
                StringLocalizer["The minimum amount is {0}", displayFormatter.Currency(settings.MinAmount, settings.Currency!, DisplayFormatter.CurrencyFormat.Symbol)]);
        if (message?.Length > settings.MaxMessageLength)
            ModelState.AddModelError($"{nameof(PublicViewModel.Donation)}.{nameof(DonationViewModel.Message)}",
                StringLocalizer["The message must not be longer than {0} characters", settings.MaxMessageLength]);
        if (!ModelState.IsValid || string.IsNullOrEmpty(message))
            return View(await GetPublicViewModel(app, donation));

        try
        {
            var orderUrl = Request.GetDisplayUrl();
            var metadata = new InvoiceMetadata
            {
                ItemCode = CheddarChatLiteApp.ItemCode,
                ItemDesc = GetTitle(settings, app.StoreData),
                OrderId = AppService.GetRandomOrderId(),
                OrderUrl = orderUrl
            };
            // Use SetAdditionalData, because replacing AdditionalData would drop the properties set above
            metadata.SetAdditionalData(CheddarChatLiteService.MetadataKey, CheddarChatLiteService.CreateMetadata(name, message));

            var request = new CreateInvoiceRequest
            {
                Amount = donation.Amount,
                Currency = settings.Currency,
                Metadata = metadata.ToJObject(),
                Checkout = new InvoiceDataBase.CheckoutOptions { RedirectURL = orderUrl },
                AdditionalSearchTerms = [AppService.GetAppSearchTerm(app)]
            };
            var invoice = await invoiceController.CreateInvoiceCoreRaw(request, app.StoreData, Request.GetAbsoluteRoot(),
                [AppService.GetAppInternalTag(app.Id)]);

            return RedirectToAction(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId = invoice.Id });
        }
        // What BTCPay Server reports to the customer, e.g. a store without a wallet. Anything else is a real error.
        catch (BitpayHttpException e)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = e.Message.Replace("\n", "<br />", StringComparison.OrdinalIgnoreCase),
                Severity = StatusMessageModel.StatusSeverity.Error,
                AllowDismiss = true
            });
            // Back to the address the form was sent to. On a mapped domain, the address with the app id would
            // redirect once more, which loses the message.
            return LocalRedirect(Request.PathBase + Request.Path);
        }
    }

    [HttpGet("/apps/{appId}/cheddarchat/overlay/{key}")]
    public async Task<IActionResult> Overlay(string appId, string key)
    {
        var app = await appService.GetApp(appId, CheddarChatLiteApp.AppType, true);
        if (app == null)
            return NotFound();

        var settings = app.GetSettings<CheddarChatLiteSettings>();
        if (!CheddarChatLiteService.IsOverlayKeyValid(settings, key))
            return NotFound();

        var brandColor = app.StoreData.GetStoreBlob().BrandColor;
        return View(new OverlayViewModel
        {
            Title = GetTitle(settings, app.StoreData),
            SocketUrl = Url.Action(nameof(OverlaySocket), new { appId = app.Id, key })!,
            ImageUrl = settings.AlertImageUrl,
            Voice = settings.TextToSpeech && CheddarChatLiteVoices.IsValid(settings.Voice) ? settings.Voice : null,
            AccentColor = !string.IsNullOrEmpty(brandColor) && ColorPalette.IsValid(brandColor) ? brandColor : DefaultAccentColor
        });
    }

    [HttpGet("/apps/{appId}/cheddarchat/overlay/{key}/ws")]
    public async Task<IActionResult> OverlaySocket(string appId, string key, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return NotFound();

        var app = await appService.GetApp(appId, CheddarChatLiteApp.AppType);
        if (app == null || !CheddarChatLiteService.IsOverlayKeyValid(app.GetSettings<CheddarChatLiteSettings>(), key))
            return NotFound();

        var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await cheddarChatService.RunOverlaySocket(app.Id, socket, cancellationToken);
        return new EmptyResult();
    }

    private async Task<PublicViewModel> GetPublicViewModel(AppData app, DonationViewModel donation)
    {
        var settings = app.GetSettings<CheddarChatLiteSettings>();
        return new PublicViewModel
        {
            AppId = app.Id,
            StoreName = app.StoreData.StoreName,
            Title = GetTitle(settings, app.StoreData),
            Description = settings.Description,
            Currency = settings.Currency,
            ButtonText = settings.ButtonText,
            ShowHeader = settings.ShowHeader,
            MinAmount = settings.MinAmount,
            MaxMessageLength = settings.MaxMessageLength,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, app.StoreData.GetStoreBlob()),
            Donation = donation
        };
    }

    private void SetNoOverlayError() =>
        TempData[WellKnownTempData.ErrorMessage] = StringLocalizer["No overlay is connected. Add the overlay URL as a browser source in OBS."].Value;

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string GetStoreDefaultCurrencyIfEmpty(StoreBlob storeBlob, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            currency = storeBlob.DefaultCurrency;
        }
        return currency.Trim().ToUpperInvariant();
    }

    private static string GetTitle(CheddarChatLiteSettings settings, StoreData store) =>
        string.IsNullOrWhiteSpace(settings.Title) ? store.StoreName : settings.Title;

    // The oldest one, in case a store got more than one through BTCPay Server's general "Create app" page
    // Archived (disabled) ones included
    private async Task<AppData?> GetStoreApp(string storeId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        return await ctx.Apps
            .Where(app => app.StoreDataId == storeId && app.AppType == CheddarChatLiteApp.AppType)
            .OrderBy(app => app.Created)
            .FirstOrDefaultAsync();
    }

    private static string GetSearchTerm(AppData app) => $"appid:{app.Id}";

    private StoreData? GetCurrentStore() => HttpContext.GetStoreDataOrNull();

    // BTCPay Server resolves any app of the store from the address. Without the type check, opening these pages
    // with the id of e.g. a Point of Sale app would overwrite its settings.
    private AppData? GetCurrentApp() =>
        HttpContext.GetAppDataOrNull() is { AppType: CheddarChatLiteApp.AppType } app ? app : null;
}
