# Roadmap

## Shipped in 0.1.0

- Windows desktop, installer, personal SQLite history, per-market scope and background tray monitoring.
- Auctionator price history, TSM snapshot import, public hourly market data, Mastercraft recipes and Forever Hive Command inventory.
- Pricing floors, undercuts, flip and investment screens, dated patch references, configurable price alerts, portfolio and recipe margins.
- Release checks, checksum validation, opt-in update installation, regression suite and versioned release workflow.

## Next testable milestones

1. Validate a full automatic update from 0.1.0 to a second published version with a populated database and portable/standard installations.
2. Calibrate forecast error and holding times against the user's recorded fills. Add out-of-sample evaluation by market and item type.
3. Add opt-in per-item depth and price-history requests with cached results, and compare listing-stack cost with likely units sold.
4. Extend profession inputs to eligible vendor prices, actual learned recipes, binding status and alternative reagent options.
5. Expand patch-to-item evidence using dated recipe or item changes and observed pre/post prices. Record intervention dates separately from hypotheses.
6. Test additional languages, regions, rare suffixes, neutral markets and future SavedVariables schemas using verified local files.
