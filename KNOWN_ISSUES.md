# Known issues and boundaries

- Recommendations model an exit at a historical asking price. Sales volume is inferred where supplied. Order-book depth, queue position and deposit loss can change the result. The app does not predict an exact time to sale or provide a calibrated probability.
- The default 5% sale-cut and 0c lost-deposit assumptions are editable. Enter house-appropriate settings; the same configured assumptions apply to all houses.
- Local snapshot adapters support verified Forever formats and base item IDs. Item modifiers and random suffixes are excluded from the TSM and Auctionator imports. The external provider aggregates its screener by item ID, so verify the exact gear variant before acting.
- Auctionator 349 could migrate earlier data that predates the faction fix. The displayed house comes from the saved database key. Local daily history cannot establish intra-day order or time of an individual observation.
- Mastercraft crafting estimates use all reagents at the current market ask. Assume access to the recipe and a tradable output. Vendor discounts, bound items, random outputs, proc yields and depth-adjusted shopping lists require further work.
- Public feed polling follows the selected house. Closing to the notification area keeps monitoring active. After exiting or signing out, the app can only collect again after restart. Run at sign-in is optional.
- Some Public-feed and official article endpoints can change shape or become inaccessible. Source health shows the failure and preserves prior observations. Roleplay house mappings are provider inferences.
- The Windows binaries are unsigned. Automatic software updates require a public release with a GitHub-provided SHA-256 digest. Verification and invalid-checksum rejection are tested. A real upgrade to a later release requires two distinct published versions.
- Notifications depend on Windows notification preferences. Multiplayer inventory, crafting and market intelligence depend on whichever add-ons the player has installed. Exchange reads personal data locally.
