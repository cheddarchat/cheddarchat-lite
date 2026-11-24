using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.CheddarChatLite.Services;
using BTCPayServer.Services.Apps;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.CheddarChatLite.HostedServices;

public class CheddarChatLiteInvoiceListener(
    EventAggregator eventAggregator,
    ILogger<CheddarChatLiteInvoiceListener> logger,
    AppService appService,
    CheddarChatLiteService cheddarChatService)
    : EventHostedServiceBase(eventAggregator, logger)
{
    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        // Same events that trigger BTCPay Server's "Invoice settled" webhook
        if (evt is not InvoiceEvent { EventCode: InvoiceEventCode.Confirmed or InvoiceEventCode.MarkedCompleted } invoiceEvent ||
            invoiceEvent.Invoice.Metadata.ItemCode != CheddarChatLiteApp.ItemCode)
            return;

        var invoice = invoiceEvent.Invoice;
        foreach (var appId in AppService.GetAppInternalTags(invoice))
        {
            var app = await appService.GetApp(appId, CheddarChatLiteApp.AppType);
            if (app != null)
                await cheddarChatService.HandleSettledInvoice(app, invoice);
        }
    }
}
