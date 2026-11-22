# Test results

## Version 0.1.1

All 43 local checks passed. The additional coverage verifies the schema-1 migration and preserved settings, newer-schema rejection, item-name boundaries, and selection of a recent dated hotfix for an eligible candidate. The release workflow now exercises ordinary per-user Windows installation and verifies its shortcut and executable checksum.

## Windows validation • November 21, 2026

`scripts/build.ps1 -Test` completed 39 regression checks. Fixtures are generated in a temporary directory with their own SQLite database. The suite does not load the installed game or write fabricated rows to a user database.

| Area | Observed result |
| --- | --- |
| Financial arithmetic | One-copper floor, conservative proceeds rounding, even-sample median, lost deposits and 2,323 break-even boundaries passed. |
| Source adapters | Base-32 TSM values, field mapping, variant exclusions, schema rejection, market isolation, Auctionator CBOR and byte escapes passed. FHC1 bag decoding passed. |
| Live polling behavior in fixtures | Changed file imported, unchanged file skipped, duplicate snapshot deduplicated, interrupted write preserved previous state, corrected write recovered. Malformed JSON and wrong-market data rejected. |
| Analysis | Minimum history, freshness gates, prior-day median, recent trend, contemporaneous TSM constraint and unknown volume handling passed. |
| Crafting and ledger | Output count, missing reagent handling, fees, weighted-average realized profit and invalid oversell rejection passed. |
| News and updates | Official article links, date normalization, RSS domain filtering, release digest match and changed-payload rejection passed. |
| Persistence and UI | Database reopened with intact trades. SQLite integrity check returned ok. All nine views constructed and navigated. |
| Scale | 10,000 generated quotes inserted and loaded into the model in 24–26 ms on the development PC in sample runs. Timing excludes GUI painting and network latency. |
| Installer | Portable install and reinstall both exited 0. Installed executable hash equaled build output. Reinstall retained existing Data files. |
| Desktop | Installed executable launched as its own native Windows window. The market selector, pricing table, row count and sync status were observed. |

## Real data validation

The installed US Normal Horde client yielded 22,010 Auctionator item-day observations between November 5 and November 21, 2026, 6,764 timestamped TSM price rows, 2,016 local recipe records and 39 inventory rows. The native desktop then fetched a public overview containing 6,764 priced items. All items in that snapshot resolved to names. At the checked snapshot, 1,136 recipes had complete market input and output prices.

Three unit asks were decoded independently from the installed TSM source and compared with the public market overview and database. Item 2589 was 17c, item 15115 was 600c and item 11038 was 298c. The public house and TSM download timestamps matched. Counts retain their source units. A TSM listing count need not equal public units listed.

A first live test exposed a mismatch with the initial Blizzard website parser. The corrected collector recognized official Forever article links and parsed ten dated hotfix entries from Blizzard's rolling forum post. Provider downtime, unknown fields, failed parsing and stale data have separate visible outcomes.

## Further validation

A second real release is needed to exercise full network upgrade installation. Live sell-through, the user's future fills and historical forecast accuracy remain unevaluated. The standard installer Start-menu and optional sign-in shortcut paths require verification in a standard installation; the verified installation used the portable mode.
