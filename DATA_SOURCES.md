# Data sources

Verified for the first release on November 21, 2026. All imports are read-only. No client credentials or game-input automation are used.

| Source | Observed contract | Scope and refresh |
| --- | --- | --- |
| Auctionator 349 | SavedVariables `AUCTIONATOR_PRICE_DATABASE`, schema 8. Each named house contains a quoted CBOR payload. Read numeric base item IDs. `h` is highest observed minimum for the day, `l` is lowest with fallback to `h`, and `a` is peak available units. The scan-day origin is January 1, 2020 in addon-local calendar time. | Read on changed file. Calendar-day precision only. Earlier builds could mix faction data before the addon's 348/349 migration. Older rows inherit the house label written by the addon. |
| TSM AppHelper | `Interface/AddOns/TradeSkillMaster_AppHelper/AppData.lua`. Accept Forever region identification and faction/ruleset-specific AUCTIONDB_NON_COMMODITY_DATA. Item IDs are decimal. Encoded metrics use base 32. Read minBuyout, numAuctions, marketValueRecent and downloadTime. | File supplied by TSM's desktop companion. `numAuctions` is listing count. TSM availability alone provides no confirmed sell-through. |
| Auction House open data | GET `https://auctionhouse.tk.gg/api/m/{key}`. Read name-indexed compact rows, per-unit copper ask, listed units and inferred 24-hour volume. Snapshot timestamps are provider milliseconds UTC. | Hourly maximum per selected house, including after failures or restarts. Live US Normal Horde verified against the local TSM snapshot. Unknown volume stays unknown. |
| Official Battle.net API | Community testing documents `/data/wow/ruleset/{id}/auctions/`, house detail and house-specific commodities under the Forever dynamic namespace. Several rulesets expose the same houses. | The public provider consumes Blizzard snapshots. Exchange does not require Battle.net OAuth credentials. |
| Forever Hive Command 0.1 | `FHCWire` tagged FHC1 percent-encoded JSON, schema 1. Bag/bank detail rows with positive item IDs, counts and section timestamps. | Read from the installed addon's SavedVariables on save. No additional addon installation on this setup. |
| Mastercraft | Read the installed addon's English ItemData and RecipeData declarations for item names, output count and fixed reagents. Game data remains on the user's PC. | Reimport on file change. The public market feed also supplies item names. No vendoring of third-party addon data or source. |
| Blizzard news | Official WoW news index, official Forever hotfix topic's first post, and official announcement and hotfix article references. Fallback discovery through WoW Campfire's RSS filters destinations to official Blizzard domains. | Check every six hours. Announcements with uncertain timing link to the article. Seeded entries carry verified effective dates. |
| GitHub releases | Public latest release metadata; exact repository download path, semantic tag, SHA-256 release-asset digest. | Daily checks, manual check from Update settings. Installation requires opt-in. |

## References

- [Hourly feed fields, house mapping, provenance and fair use](https://auctionhouse.tk.gg/data/)
- [Forever API endpoint investigation](https://us.forums.blizzard.com/en/blizzard/t/wow-forever-api-what-works-so-far-plus-a-ts-client/60334)
- [Auctionator build 349](https://www.curseforge.com/wow/addons/auctionator/files/9690303/additional-files) and [build 348 faction fix](https://www.curseforge.com/wow/addons/auctionator/files/9688385)
- [TradeSkillMaster public data](https://tradeskillmaster.com/public-data); the installed AppHelper and AuctionDB Lua definitions establish the local decoding contract.
- [Blizzard's Forever roadmap and raid unlock](https://worldofwarcraft.blizzard.com/en-us/news/24303862/world-of-warcraft-forever-whats-next)
- [Official rolling hotfixes](https://us.forums.blizzard.com/en/wow/t/wow-forever-hotfixes-november-20-2026/2403040)
- [Blizzard news index](https://news.blizzard.com/en-us/feed/world-of-warcraft) and [WoW Campfire's Forever tracker](https://wowcampfire.com/blue-tracker)
- [GitHub release asset metadata](https://docs.github.com/en/rest/releases/assets)
