# Database-file restore

Database-file restore stages the supplied bytes in a temporary directory before changing the live database. It opens the candidate through SQLite, runs integrity_check, requires the eight C# application tables, applies existing C# compatibility upgrades to the candidate, and materializes each entity type to detect incompatible columns and invalid serialized values. It checks foreign_key_check plus explicit customer, product and invoice references because some current relationships lack SQLite foreign-key constraints.

Only validated candidates reach SQLite's BackupDatabase operation on the existing destination. The live database is no longer deleted or overwritten with raw file bytes. SQLite's [online backup API](https://www.sqlite.org/c3ref/backup_finish.html) holds a write transaction on the destination during the copy. Source/destination connections used by this operation disable pooling and are disposed before staging cleanup. Successful restore clears the current session and reloads application data; rejected validation preserves the live data and session.

Regression checks cover empty, non-SQLite and truncated inputs; missing account tables; malformed invoice snapshot JSON; missing customer references; successful restore; and reopening restored data. Each rejected input is followed by checks of existing invoice totals, account credentials and session validity.

This does not establish Flutter database compatibility, complete financial-data validation, or a complete disaster-recovery guarantee. Disk-full, power-loss, destination-lock contention, cleanup failure and concurrent application writes still need dedicated release-environment drills. Full restore authorization and stronger versioned schema migrations remain open. JSON restore follows its separate existing transaction path.

## JSON restore validation

JSON restore requires all seven exported business tables as arrays before opening its replacement transaction. Missing/null tables, null records, malformed roots/metadata, unsupported declared versions, nonpositive or duplicate IDs, duplicate/blank setting keys and broken record references are rejected. Complete older exports without metadata remain accepted; complete exports containing explicitly empty arrays remain valid. An empty object is no longer treated as an empty database backup. Accounts are retained by the existing JSON restore path. This validation checks structure and relationships, not complete financial consistency or Flutter export compatibility.

## JSON restore failure injection

The regression suite installs a temporary SQLite trigger that aborts invoice-item insertion after restore has deleted the prior rows and begun replacing them. It verifies that the outer transaction restores the original invoice total, item, settings and account, then drops the trigger and retries successfully. A separate injected context-creation failure verifies that database setup errors return a failed restore result rather than escaping the UI action. Transaction and context creation are now inside the restore error handler. These tests do not simulate disk exhaustion or process/power loss.

## Destination write-lock drill

Restore connections now use a two-second SQLite lock timeout. The regression suite holds a separate write transaction on the live database while attempting to restore an older backup. It verifies failure preserves the newer invoice total and session, staging cleanup completes, and retry after releasing the lock applies the older backup and clears authentication. This covers a competing writer already holding the destination lock, not arbitrary concurrent writes across all application operations. The timeout bounds lock waiting, not total validation or copy duration.

## Result reporting after database copy

Once the database copy commits, a workspace reload failure reports that data was restored and asks the user to restart; it does not return a failed-copy result. The session remains cleared and the Settings page is not rebuilt for signed-out users. Staging cleanup IO/access failures cannot replace the copy outcome: after success the result includes a cleanup warning and folder path; during an existing copy failure the original exception remains primary. An injected context failure after copying verifies committed data, the restart message, signed-out state and reopening. Actual cleanup access failures still require platform-specific fault injection.

JSON restore now also separates its committed transaction from later workspace reload. A reload exception returns a successful restore result with restart guidance, clears session access and prevents the restore screen from rebuilding Settings. A factory failure injected after one successful context creation verifies the replacement was committed and can be read after reopening. Successful JSON reload retains the existing account/session behavior.

## Pending file-picker operations

Backup/export and restore UI callbacks capture their starting model and session version. They verify access before opening pickers and recheck after picker completion, before writing backup content or applying restored content. Logout/login cycles, external account changes and model replacement invalidate the pending callback. This prevents an old picker from continuing under a different session. It does not cancel IO already in progress, guarantee an opened destination file is untouched, or provide service-level authorization. Session-continuation tests cover signed-out/forced-change states, normal continuation, logout, same-account relogin and external account changes; native picker automation remains pending.

## Backup export stream handling

JSON and database exports reset/truncate seekable destination streams, write the complete payload, flush, and dispose the stream before reporting success. This prevents trailing bytes when a provider opens an existing longer file without truncation. Backup file actions handle IO/access errors without an unhandled asynchronous UI exception. Tests cover shorter replacement, empty replacement, an unwritable destination and injected flush failure. Non-seekable stream replacement semantics remain provider-dependent. This is not atomic file replacement: interrupted writes can leave a partial destination, and actual disk-full/native-provider testing remains open.
