# Crown Shop implementation and checks

The server builds a catalog from locally installed item templates. It sends the
native tabs/categories, recommendations, eligibility data, and localized labels.
All catalog entries have a gold price equal to ten times their crown price.
New accounts and accounts without a crown balance receive 10,000 crowns once.
Zero balances are preserved. These are emulator economy defaults, not a replica
of the official game's pricing or eligibility policies.

The catalog is paged to avoid the pinned codec's incompatible large-packet path:

- `.crownshop page 2` switches catalog pages and clears a search.
- `.crownshop search <name or template ID>` searches the entire catalog.
- Reopen the Crown Shop after changing pages/search. Its own search covers only
  the current page.

Purchases validate a consumed price lock, the currency/price, and inventory or
stack capacity. Debit and supported rewards are committed together in RavenDB.
Gifting is disabled. Supported delivery includes ordinary items, pets, bundles,
packs with available loot tables, reagents, snacks, spell rewards, currency,
instant refills, character slots, and world-completion elixirs. Unsupported
services and missing reward tables are declined before charging.

## World elixirs

All 11 installed world-elixir templates (Celestia through Novus) use their quest
completion lists and prerequisite chain. A selected elixir completes earlier
worlds too. The first arc reuses progression/spells from the Level 50 template,
without its gear, gold, or optional Grizzleheim quests. Unrelated quests remain.
The level floor is 60 for Celestia, increasing by ten per world through Novus 160;
higher levels/XP are preserved. Level-based training points and spells explicitly
listed by the templates are granted. Already-completed targets are rejected.
Combat or a locked level prevents purchase. The final destination is taken from
the template, and the server requests a transfer after the purchase commits.

Most later-world quest definitions/rewards are absent from the available server
database. Completion flags and the level floor do not implement missing quest
scripts, NPC behavior, or unspecified quest rewards. Standalone level-up elixirs
and timed effects remain unsupported.

## Client compatibility

The private r806919.Wizard_1_610 client disables gold selection for templates
flagged `FLAG_CrownsOnly`, even when a positive gold price is sent. The optional
`tools/CrownShopClient/enable_gold.py` patch changes that Crown Shop check, backs
up the original executable, and rejects unknown signatures. Restart the client
after applying it. No client executable or archive is distributed here.

The server-side item serializer corrects the pet-snack class-hash envelope while
using the repository's unchanged Imcodec submodule revision.

## Run checks

With .NET 10 and your own installed `Root.wad` and adjacent
`_Shared-WorldData.wad`:

```sh
dotnet build src/Imlight.Director -c Release
dotnet run --project tools/CrownShopCheck -c Release -- /path/to/Root.wad
```

For production resource initialization, database transaction, pack, and world
elixir checks, also supply a reachable RavenDB URL and local SpiralDB checkout:

```sh
dotnet run --project tools/CrownShopCheck -c Release -- /path/to/Root.wad http://localhost:18080 /path/to/spiraldb
```

The database checks create a uniquely named temporary database, remove it in a
`finally` block, and do not modify the server's player database. They cover all
77 world/school combinations, both currencies, successive/repeated purchases,
insufficient funds, persistence, and unrelated quest ownership. Transport tests
cover combined and fragmented handshake/attach frames. Catalog checks verify
prices, membership, localized labels, packet sizes/framing, and serialization.

The production startup singleton fix is a prerequisite for world purchases;
otherwise accessing the spell singleton after startup throws a duplicate-key
exception. The full tests intentionally exercise that startup sequence.
