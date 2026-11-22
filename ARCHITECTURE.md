# Architecture

## Runtime

C# compiled with Windows' .NET Framework compiler targets Windows desktop. WinForms supplies tables, charts and tray notifications. SQLite comes from the operating system's winsqlite3.dll. No package installation or application server is required. The installer carries the executable and README as embedded resources.

## Components

| File | Responsibility |
| --- | --- |
| src/App.cs | GUI and CLI entry points, single-instance activation, standard and portable database paths. |
| src/Database.cs | UTF-8 SQLite adapter, schema creation, transactions and connection lifecycle. |
| src/LocalSources.cs | Bounded data-only readers for Auctionator v8, TSM, FHC1 and Mastercraft. |
| src/Network.cs | Rate-limited HTTPS collection, market identity checks, official news discovery and dated hotfix ingestion. |
| src/Economy.cs, src/Analysis.cs | Copper arithmetic, prior-day aggregation, recommendation criteria, ledger and crafting margins. |
| src/Desktop.cs | Market selector, interactive tables, price history, inventory, settings, trades, alerts and polling. |
| src/Updates.cs, installer/Setup.cs | Release validation, atomic executable replacement, shortcuts and startup. |
| scripts/build.ps1, tests/Tests.cs | Dependency-free builds, installer and regression checks. |

## Storage and consistency

Market keys include region, ruleset and house. Quote identity includes market, source, source timestamp and item. Daily history has one record per market, item and observed calendar day. Overlapping daily observations retain price extremes and peak supply. Quotes and file metadata commit together. An invalid input keeps the previous successful state and is retried on the next poll. Network attempts have a persisted cooldown. Concurrent readers use WAL and a ten-second busy timeout.

The screener selects one complete timestamped source snapshot per market. When timestamps tie, the public feed supplies item names and inferred volume. A TSM recent-value observation within one hour can constrain the resale model. Unlisted items drop out of that snapshot. Historical rows remain queryable. The fallback with only Auctionator displays daily observations and suppresses time-sensitive actions.

Item-day history uses a rolling 21-day window before the snapshot day. Within each prior day, the lowest observed ask wins. Baseline is the median of those day lows. The exit target is one copper below the baseline or available lower recent TSM reference. Monetary proceeds are rounded down after subtracting the modeled auction cut. Expected failed deposits are costed per unit. Historical medians exclude the current day. The six-hour freshness window gates flips, investment signals, alerts and craft estimates.

Portfolio buys add paid fees to basis. Sales remove proportional basis and subtract actual sale fees. Inventory is an independently observed character snapshot. It is never treated as a portfolio purchase.

## Reproduction

Schema version 2 adds dated individual hotfix notes. Startup applies the schema inside one write transaction and rejects an unsupported newer version. Flip and investment candidates can reference an exact item-name mention in the preceding 30 days. Matching uses word boundaries and the current locale-independent item name.

Run `scripts/build.ps1 -Test` with PowerShell on Windows. For headless collection use `ForeverHiveExchange.exe --sync <database-path> <_forever_-folder>`. Use `--summary <database-path>` for counts and integrity. PowerShell can use Start-Process with -Wait for the GUI-subsystem executable. Starting without arguments runs the desktop. The --tray option starts hidden.

Changing VERSION on main runs the Windows release workflow. Keep installer and assembly versions aligned, rerun tests, commit source, change VERSION, and verify the release job and both assets. The app trusts the public repository as its release authority; publisher signing is a separate follow-up.
