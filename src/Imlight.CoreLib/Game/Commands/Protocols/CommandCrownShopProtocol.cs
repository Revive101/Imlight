using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Items;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandCrownShop : CommandProtocol {
    internal override string Group { get; set; } = "crownshop";

    [Command("page")]
    private void Page(string value) {
        if (!int.TryParse(value, out var page)) {
            InformSenderClient("Use .crownshop page <number>.");
            return;
        }
        Context.SessionActor.Tell(new SERVICE_101_PROTOCOL.MSG_CROWNSHOPPAGE { Page = page });
        var pages = (CrownShopCatalog.Instance.Entries.Count + CrownShopCatalog.PageSize - 1) / CrownShopCatalog.PageSize;
        InformSenderClient($"Crown Shop catalog page {System.Math.Clamp(page, 1, System.Math.Max(1, pages))}/{pages}. Open the Crown Shop to browse. Use .crownshop search <name> to find items.");
    }

    [Command("search")]
    private void Search([Remainder] string name) {
        Context.SessionActor.Tell(new SERVICE_101_PROTOCOL.MSG_CROWNSHOPPAGE { Search = name });
        InformSenderClient($"Crown Shop filtered by '{name}'. Open the shop to browse. .crownshop page 1 clears the filter.");
    }
}
