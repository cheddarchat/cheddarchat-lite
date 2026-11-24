using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Plugins.CheddarChatLite.Controllers;
using BTCPayServer.Plugins.CheddarChatLite.Services;
using BTCPayServer.Services;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.CheddarChatLite;

public class CheddarChatLiteApp : AppBaseType, IHasSaleStatsAppType, IHasItemStatsAppType
{
    private readonly LinkGenerator _linkGenerator;
    private readonly DisplayFormatter _displayFormatter;
    private readonly CurrencyNameTable _currencies;
    private readonly IOptions<BTCPayServerOptions> _btcPayServerOptions;
    public const string AppType = "CheddarChatLite";
    public const string ItemCode = "cheddarchat";

    private static readonly AppItem[] Items =
    [
        new()
        {
            Id = ItemCode,
            Title = "CheddarChat",
            PriceType = AppItemPriceType.Topup
        }
    ];

    public CheddarChatLiteApp(
        LinkGenerator linkGenerator,
        DisplayFormatter displayFormatter,
        CurrencyNameTable currencies,
        IOptions<BTCPayServerOptions> btcPayServerOptions)
    {
        Type = AppType;
        Description = "CheddarChat Lite";
        _linkGenerator = linkGenerator;
        _displayFormatter = displayFormatter;
        _currencies = currencies;
        _btcPayServerOptions = btcPayServerOptions;
    }

    public override Task<string> ConfigureLink(AppData app)
    {
        return Task.FromResult(_linkGenerator.GetPathByAction(
            nameof(UICheddarChatLiteController.UpdateSettings),
            nameof(UICheddarChatLiteController).TrimEnd("Controller", StringComparison.InvariantCulture),
            new { appId = app.Id }, _btcPayServerOptions.Value.RootPath)!);
    }

    public override Task<object?> GetInfo(AppData appData)
    {
        return Task.FromResult<object?>(null);
    }

    public override Task SetDefaultSettings(AppData appData, string defaultCurrency)
    {
        var settings = new CheddarChatLiteSettings
        {
            Currency = defaultCurrency,
            MinAmount = GetDefaultMinAmount(defaultCurrency),
            OverlayKey = CheddarChatLiteService.CreateOverlayKey()
        };
        appData.SetSettings(settings);
        return Task.CompletedTask;
    }

    // The smallest amount of the currency, e.g. 0.01 USD: without a minimum, an alert costs next to nothing
    private decimal GetDefaultMinAmount(string currency) =>
        _currencies.GetCurrencyData(currency, false) is { Divisibility: >= 0 and <= 28 } data
            ? new decimal(1, 0, 0, false, (byte)data.Divisibility)
            : 0m;

    public override Task<string> ViewLink(AppData app)
    {
        return Task.FromResult(_linkGenerator.GetPathByAction(
            nameof(UICheddarChatLiteController.Public),
            nameof(UICheddarChatLiteController).TrimEnd("Controller", StringComparison.InvariantCulture),
            new { appId = app.Id }, _btcPayServerOptions.Value.RootPath)!);
    }

    public Task<AppSalesStats> GetSalesStats(AppData app, InvoiceEntity[] paidInvoices, int numberOfDays)
    {
        return AppService.GetSalesStatswithPOSItems(Items, paidInvoices, numberOfDays);
    }

    public Task<IEnumerable<AppItemStats>> GetItemStats(AppData appData, InvoiceEntity[] paidInvoices)
    {
        var item = Items.First();
        var settings = appData.GetSettings<CheddarChatLiteSettings>();
        var itemCount = paidInvoices
            .Where(entity => entity.Metadata.ItemCode == ItemCode &&
                             entity.Currency.Equals(settings.Currency, StringComparison.OrdinalIgnoreCase))
            .Aggregate([], AppService.AggregateInvoiceEntitiesForStats(Items))
            .GroupBy(entity => entity.ItemCode)
            .Select(entities =>
            {
                var total = entities.Sum(entity => entity.FiatPrice);
                var itemCode = entities.Key;
                return new AppItemStats
                {
                    ItemCode = itemCode,
                    Title = item.Title ?? itemCode,
                    SalesCount = entities.Count(),
                    Total = total,
                    TotalFormatted = _displayFormatter.Currency(total, settings.Currency)
                };
            })
            .OrderByDescending(stats => stats.SalesCount);
        return Task.FromResult<IEnumerable<AppItemStats>>(itemCount);
    }
}
