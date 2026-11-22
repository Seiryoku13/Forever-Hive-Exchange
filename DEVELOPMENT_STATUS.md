# Development status

## 0.1.1

The second Windows build adds source-linked item-level hotfix context and an atomic schema update. The regression suite now runs 43 checks. The v0.1.0 installation continued collecting while unattended for several hours, through multiple changes to the public and local TSM snapshots.

## 0.1.0 • November 21, 2026

The Windows desktop, installer and 39-check suite build locally. A portable installation opened successfully and collected actual US Normal Horde data. The latest native run received the public overview, Blizzard news and dated hotfix notes. Market history, portfolio, inventory and settings persist in SQLite. Account data, personal inventory, databases and game files are excluded from git and release artifacts.

## Resume from here

1. Open the current main branch and inspect VERSION and the GitHub release job for its matching commit.
2. Run `scripts/build.ps1 -Test` and check TEST_RESULTS.md for the distinction between generated cases and real data. Generated tests can run without WoW.
3. Launch the installed desktop, select Sources and verify the file-based collectors and independent remote feeds. A selected market can have a valid empty or stale snapshot.
4. Keep price history in Data and use the full folder for backups while the program is exited. No private game files are needed to build or publish.
5. Use ROADMAP.md and KNOWN_ISSUES.md for work that requires a second release, new source data, additional markets or observed sales.

The standard monitoring interval is one minute for local file changes, one hour for a selected public market, six hours for official news and one day for software versions. Application exit stops monitoring. The notification-area menu and optional sign-in shortcut support longer-running collection.
