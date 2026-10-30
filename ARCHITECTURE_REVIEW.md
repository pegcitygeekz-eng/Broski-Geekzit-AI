# Broski AI Architecture Review

## Scope and method

Reviewed maintained desktop and Android source, installer and build inputs, manager/model scaffolding, and the original 30 AIData JSON files. Generated `bin` and `obj` files were excluded. All original JSON files were backed up before conversion. The active `AIData` directory now contains 20 valid JSON files.

The source backup is `AIData-backup-0.0.3-20261030-023103-55d12751a93940328626a683934ddad9`. Runtime upgrades create a per-user snapshot under `%LOCALAPPDATA%\\ChoppaAI\\backups` before migrating existing local AIData. Retired files are moved into that backup after successful validation; they are not discarded.

## Current architecture

- The Windows WPF app owns onboarding, chat/provider calls, settings, animation, device detection, media controls, and organizer orchestration in `MainWindow.xaml.cs`.
- `AiDataMemoryStore` owns per-user JSON memory under `%LOCALAPPDATA%\\ChoppaAI\\AIData`; typed managers now handle recall/index operations, relationships, and runtime-state updates.
- `OrganizerDataStore` owns alarms, appointments, and device records under `%LOCALAPPDATA%\\BroskiAI\\organizer.json`. `WindowsAlarmScheduler` creates/removes corresponding Windows scheduled tasks.
- App settings, provider secrets, and DPAPI-protected learned interactions remain separate files under `%LOCALAPPDATA%\\ChoppaAI`. The protected learned-interaction file is distinct from `AIData\\knowledge.json`.
- Android remains a separate app with its own Ollama client and SharedPreferences; it shares `BasicPhraseResponder`, not desktop storage.
- The new standalone `BroskiLauncher.exe` starts the co-located `Broski.exe`; installer shortcuts and startup launch use the launcher.

## Runtime and persistence findings

- The memory store loads 19 active AIData documents: `brain.json`, `memory.json`, `knowledge.json`, `projects.json`, `topics.json`, `conversations.json`, `notes.json`, `tasks.json`, `relationships.json`, `recallindex.json`, `memorytimeline.json`, `analytics.json`, `bookmarks.json`, `runtimestate.json`, `specialusers.json`, `userprofile.json`, `history.json`, `recent.json`, and `learning.json`.
- `tutorial.json` is the 20th active packaged AIData file and is read by onboarding.
- `history.json` and `recent.json` were previously accessed without being loaded. They are now managed by the store, restoring interaction-history and recent-item writes without changing their schema.
- `userprofile.json` is loaded and rewritten but no read-side consumer of its stored `Name` was found. It remains in place for compatibility.
- `LanguageProcessor` reads the consolidated `learning.json` maps. The processor has no caller in the reviewed source, so automatic normalization of user chat input is not enabled.
- Other runtime JSON includes `settings.json`, `ai-providers.json`, DPAPI-protected `knowledge.json`, organizer data, and the installer manifest.

## Problems found and addressed

1. **Unloaded history/recent data:** added the files to the managed memory store, avoiding invalid-operation failures when their existing write paths run.
2. **Unused and duplicate AIData templates:** consolidated 11 source files into their owning documents and reduced the active AIData package to 20 files. Original templates are in the source backup; existing per-user files are backed up and archived during versioned migration.
3. **Empty manager scaffolding and schema mismatch:** implemented recall, relationship, and runtime-state managers; aligned models with actual `Entries`, `Description`, `Timestamp`, `FromId`, `ToId`, and runtime-state JSON contracts. Recall unknown fields are preserved when manager operations rewrite entries.
4. **Monolithic UI orchestration:** `MainWindow` remains oversized. Further extraction into provider/settings/onboarding/media/organizer services is recommended as incremental work; this change avoids risky broad UI rewrites.
5. **Alarm dual-write:** organizer JSON and Windows Task Scheduler can still diverge after partial failures; existing compensation remains. A durable reconciliation check is a future improvement.
6. **Write consistency:** the memory and organizer stores use temporary files, while some provider/settings writes remain direct. Atomic writes should be applied consistently in a later focused change.

## AIData disposition, file by file

`MERGE` entries below have been consolidated into canonical files. Source originals are retained in the pre-conversion backup. No user data is intentionally discarded.

| Disposition | Original file | Current destination or rationale |
|---|---|---|
| MERGE | `aliases.json` | `learning.json:Aliases`; LanguageProcessor consumes the map. |
| KEEP | `analytics.json` | Canonical usage, activity, and achievement statistics. |
| KEEP | `bookmarks.json` | Canonical bookmark/favorite records used by memory recall. |
| KEEP | `brain.json` | Learning configuration; includes the merged `Personality` and `SearchProfiles` sections. |
| MERGE | `chattemplate.json` | `conversations.json:LegacyTemplate`; retained as data without a separate file. |
| MERGE | `commands.json` | `learning.json:Commands`. |
| KEEP | `conversations.json` | Canonical saved conversations; includes the legacy template section. |
| MERGE | `corrections.json` | `learning.json:Corrections`. |
| KEEP | `history.json` | Active interaction/entity history; managed by the store. |
| MERGE | `index.json` | `recallindex.json:LegacyCatalog`; system catalog entries now point to consolidated sections. |
| MERGE | `intents.json` | `learning.json:Intents`; search phrase data retained. |
| KEEP | `knowledge.json` | Canonical AIData knowledge store; separate from encrypted learned-interaction storage. |
| KEEP | `memory.json` | Working/session memory; retained separately from durable runtime state for compatibility. |
| KEEP | `memorytimeline.json` | Canonical chronological event and milestone history. |
| KEEP | `notes.json` | Canonical saved notes. |
| MERGE | `notifications.json` | `runtimestate.json:Notifications`; contents retained for future notification integration. |
| MERGE | `personality.json` | `brain.json:Personality`; greeting and response configuration retained. |
| KEEP | `projects.json` | Canonical project records. |
| KEEP | `recallindex.json` | Canonical recall/search index and legacy system catalog. |
| KEEP | `recent.json` | Active bounded recent-item lists; managed by the store. |
| KEEP | `relationships.json` | Canonical relationship graph. |
| KEEP | `runtimestate.json` | Canonical session, user, context, UI, and runtime flags; includes merged workspace and notification definitions. |
| MERGE | `searchprofiles.json` | `brain.json:SearchProfiles`; behavior/configuration retained. |
| MERGE | `slang.json` | `learning.json:Slang`. |
| KEEP | `specialusers.json` | Special-user identity and greeting behavior. |
| KEEP | `tasks.json` | Canonical tasks. |
| KEEP | `topics.json` | Canonical topics. |
| KEEP | `tutorial.json` | Active onboarding steps; remains a packaged file. |
| KEEP | `userprofile.json` | Canonical user profile compatibility document. |
| MERGE | `workspaces.json` | `runtimestate.json:WorkspaceDefinitions`; workspace definitions retained alongside current workspace. |

`chattemplate.json` was not removed as user data: its full content is embedded in the conversations document and backed up. The 11 retired filenames are no longer active templates; their contents remain in the canonical documents and backups.

## Final AIData structure (20 files)

**Memory:** `brain.json`, `memory.json`, `knowledge.json`, `learning.json`

**Organization:** `projects.json`, `topics.json`, `tasks.json`, `notes.json`, `conversations.json`

**Discovery:** `recallindex.json`, `relationships.json`, `memorytimeline.json`, `bookmarks.json`, `history.json`, `recent.json`

**Live state:** `runtimestate.json`

**Personality and onboarding:** `personality` configuration is in `brain.json`; special-user configuration is in `specialusers.json`; profile data is in `userprofile.json`; onboarding content is in `tutorial.json`.

**System:** `analytics.json`

This layout has 20 active JSON files and keeps the existing feature owners available. Some sections (notifications, search profiles, personality, workspaces) are retained for compatibility even where no active UI reader exists.

## Migration and safety

For existing local data, the 0.0.3 memory store:

1. Loads canonical files and checks whether the idempotent consolidation marker is present.
2. Before migrating any legacy local files, copies the full local AIData directory to a timestamped backup outside the active AIData folder.
3. Merges each legacy source into its canonical section. Existing target values win; conflicting legacy values are recorded in `MigrationConflicts`, and the complete original is also in the backup.
4. Writes canonical documents and validates them as JSON.
5. Marks migration complete and moves retired source files into the backup's `retired-source-files` directory.

Packaged templates were similarly backed up before conversion. A malformed legacy local file causes an explicit migration error after backup rather than being silently dropped.

## Runtime flows

### Memory and conversation

The app loads the memory store, starts runtime session state, and records interactions. At session close, it archives the transcript as a conversation entity. DPAPI-protected learned interactions remain a separate privacy-scoped store.

### Recall and search

Canonical entity collections and timeline records rebuild `recallindex.json`. `RecallManager` performs keyword scoring and context lookup using the typed recall schema. `RelationshipManager` reads/writes typed graph links; related titles are resolved through the recall index.

### Project/topic memory

Creating an entity stores it in the canonical collection, adds a typed recall record, emits a timeline event, updates relationships and analytics, and updates active runtime context. Project/topic/context summaries are then made available to assistant prompts.

### Timeline

Entity creation emits a typed timeline event and updates current period buckets, recent events, statistics, and entity history. Existing timeline and milestone records are preserved during index rebuilds.

## Installer and launcher

- Product version is `0.0.3` across the desktop app and setup bootstrapper.
- `installer\\Build-Installer.ps1` publishes the self-contained WPF application and standalone launcher, stages both with assets and uninstall support, then embeds the payload in `broski-0.0.3.exe`.
- The installer validates the launcher and app payload, installs both, and creates Start menu, desktop, and startup shortcuts targeting `BroskiLauncher.exe`.
- The launcher verifies `Broski.exe` exists and starts it from its installation directory, forwarding arguments and displaying actionable errors.
- The stable uninstall registry/product identity is retained so upgrades from the previous release can locate and replace their owned files. User data under LocalAppData is outside the install manifest and is not removed by uninstall.
