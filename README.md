# Forever Hive Exchange

A Windows auction-market desktop for World of Warcraft: Forever. It reads the installed Forever client's saved data, retains market history in SQLite and models gold-making opportunities for one house at a time. Buying, posting and scanning in game remain player actions.

## Install and launch

Download **ForeverHiveExchange-Setup.exe** from a project release and run it. Setup installs for your Windows user, adds **Forever Hive Exchange** to Start and launches the desktop. Windows 10 or 11 with .NET Framework 4.8 and the Windows SQLite library is required. The executable and installer are unsigned. The portable executable is also a release asset.

The initial destination is `%LOCALAPPDATA%\Forever Hive Exchange\App`. Your database is `%LOCALAPPDATA%\Forever Hive Exchange\Data\market.sqlite`. To use a portable installation, run the installer from PowerShell as follows.

```powershell
.\ForeverHiveExchange-Setup.exe --portable --target "C:\Games\ForeverHiveExchange"
```

Portable installations keep a Data folder next to the executable. To back up, exit Exchange and copy the entire Data folder. Reinstalling preserves it. To remove the app, close it and remove the executable directory and its Start shortcut. Keep Data to retain history and trades.

## First use

1. Open **Sources**. Exchange looks in the standard Battle.net World of Warcraft installation. Use **WoW folder** for another installation and select its `_forever_` directory.
2. Choose a house in the upper right. The app selects the latest previously imported house on first start with an existing database. An empty database starts at US / normal / horde. Public roleplay house labels are inferred by the feed provider.
3. For local market updates, keep the TSM desktop companion syncing its AppHelper file or run an Auctionator scan in game and use `/reload` or log out to flush SavedVariables. Exchange reads changed files every minute while running, including in the notification area after closing the window. Use Sources → Exit or the tray menu to stop monitoring. Sources → Run at sign-in enables optional background startup. Each public house is checked at most hourly. **Sources** shows errors and last contact per feed. In a new installation, allow one collection cycle for the first import.
4. Search **Market** by name or numeric item ID. Double-click a row for a price chart and enter your actual acquisition cost for the posting floor. Daily low and high are extremes of observed minimum asks. TSM counts auctions; the public feed counts units. The date shown is the provider's timestamp.
5. Select **Pricing settings** to enter your cut in basis points and expected nonrefundable deposit cost per unit. The initial scenario uses 5% and 0c. Set the figures for the house and strategy you use. These settings apply across markets. One gold equals 10,000 copper.

## Use the market views

| View | How to use it |
| --- | --- |
| Market | Read the cheapest unit ask, suggested one-copper undercut and historical median. Check the live listing, item variant and quantity in game before posting. The newest complete supported source snapshot drives the view. |
| Flips | Candidates require three previous observation days and at least 15% modeled net ROI. Target uses the lower of historical daily-low median and TSM recent value when available. Unknown sales velocity appears explicitly. |
| Investments | Candidates require seven prior observed days, a recent median at least 5% above the preceding median and at least 10% modeled net ROI. Patch links identify demand hypotheses. A predicted return assumes a later fill at the modeled target. |
| Crafting | Compares reagent cost at current market asks with net output value at the undercut. Rows require every input and the output to have a price. Check recipe ownership, output tradeability, vendor alternatives and market depth. Catalog comes from your installed Mastercraft addon. |
| Portfolio | Enter completed buys and sells in copper. Record actual fees for the whole trade. Open basis uses weighted-average cost. Realized P&L only changes when you record sales. |
| Inventory | Shows the most recently saved bag and bank rows from the Forever Hive Command addon, including character and observation time. |
| Price alerts | Set an item ID and maximum unit price. Exchange sends a Windows notification once per qualifying fresh snapshot while running. Windows notification settings control visibility. |
| Patch watch | Read dated official changes and announcements. Open an article for timing. The app checks Blizzard's news index every six hours and keeps discovered Forever article links. |
| Sources | Inspect sync health, change the game directory, sync changed files, or enable automatic application updates. |

## Repeatable workflows

From version 0.1.1, price history links screened items to recent official hotfixes that mention the item by name. The link includes the change date. A match supplies context for reviewing demand. The published price and ROI calculations still use the observed house data.

- **Reagent restocking.** Watch materials you use regularly. Establish a price cap from the chart, then check current listings when an alert fires. Enter the fill in Portfolio so subsequent sales use your actual cost.
- **Small-lot reversion flips.** Use Flips, inspect the chart, and check how many units are ahead of your target in game. Use a small quantity when volume is unknown. Include likely lost deposits and record the actual result.
- **Craft to order or to observed demand.** Filter Crafting by profession, check tradeability and compare reagent purchases with vendor prices. Check that enough units are available at the displayed asks before buying a full batch.
- **Upcoming content.** Use Patch watch to confirm dates. Follow consumables with corroborating movement in your own house and a price alert. Projected profit is per unit and excludes unmodeled relists and changes in demand.

## Updates and development

Update checks use the public repository's latest stable GitHub release. In **Sources → Update settings**, choose automatic installation or notification only. Automatic mode verifies the release asset's SHA-256 digest and installs on explicit exit. With network access unavailable, imports continue from local files.

Build and run the isolated tests on Windows PowerShell:

```powershell
.\scripts\build.ps1 -Test
.\bin\ForeverHiveExchange.exe
```

The project includes architecture, verified source formats, release status, test results, limitations and roadmap in the repository root. No external NuGet packages or runtime services are needed for local collection.

[Source repository](https://github.com/Seiryoku13/Forever-Hive-Exchange) · [Release downloads](https://github.com/Seiryoku13/Forever-Hive-Exchange/releases) · [Public market feed and schema](https://auctionhouse.tk.gg/data/)
