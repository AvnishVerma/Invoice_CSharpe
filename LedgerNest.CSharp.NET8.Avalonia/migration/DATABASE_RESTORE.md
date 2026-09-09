# Database-file restore

Database-file restore stages the supplied bytes in a temporary directory before changing the live database. It opens the candidate through SQLite, runs integrity_check, requires the eight C# application tables, applies existing C# compatibility upgrades to the candidate, and materializes each entity type to detect incompatible columns and invalid serialized values. It checks foreign_key_check plus explicit customer, product and invoice references because some current relationships lack SQLite foreign-key constraints.

Only validated candidates reach SQLite's BackupDatabase operation on the existing destination. The live database is no longer deleted or overwritten with raw file bytes. SQLite's [online backup API](https://www.sqlite.org/c3ref/backup_finish.html) holds a write transaction on the destination during the copy. Source/destination connections used by this operation disable pooling and are disposed before staging cleanup. Successful restore clears the current session and reloads application data; rejected validation preserves the live data and session.

Regression checks cover empty, non-SQLite and truncated inputs; missing account tables; malformed invoice snapshot JSON; missing customer references; successful restore; and reopening restored data. Each rejected input is followed by checks of existing invoice totals, account credentials and session validity.

This does not establish Flutter database compatibility, complete financial-data validation, or a complete disaster-recovery guarantee. Disk-full, power-loss, destination-lock contention, cleanup failure and concurrent application writes still need dedicated release-environment drills. Full restore authorization and stronger versioned schema migrations remain open. JSON restore follows its separate existing transaction path.

## JSON restore validation

JSON restore requires all seven exported business tables as arrays before opening its replacement transaction. Missing/null tables, null records, malformed roots/metadata, unsupported declared versions, nonpositive or duplicate IDs, duplicate/blank setting keys and broken record references are rejected. Complete older exports without metadata remain accepted; complete exports containing explicitly empty arrays remain valid. An empty object is no longer treated as an empty database backup. Accounts are retained by the existing JSON restore path. This validation checks structure and relationships, not complete financial consistency or Flutter export compatibility.
