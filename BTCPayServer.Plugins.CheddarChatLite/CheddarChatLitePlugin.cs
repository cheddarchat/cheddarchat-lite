using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.CheddarChatLite.HostedServices;
using BTCPayServer.Plugins.CheddarChatLite.Services;
using BTCPayServer.Services.Apps;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.CheddarChatLite;

public class CheddarChatLitePlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    [
        new() { Identifier = nameof(BTCPayServer), Condition = ">=2.4.2" }
    ];

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<AppBaseType, CheddarChatLiteApp>();
        services.AddSingleton<CheddarChatLiteService>();
        services.AddHostedService<CheddarChatLiteInvoiceListener>();
        services.AddUIExtension("header-nav", "CheddarChatLiteNavExtension");
    }
}
