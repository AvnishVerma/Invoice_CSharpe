# Multi-database deployment

On a new installation, LedgerNest displays **Database setup** before the main window opens. Choose SQLite for a single local workstation, or MySQL / SQL Server for a shared deployment. The selected profile is stored at `%LocalAppData%\LedgerNest\database-profile.json`.

Existing SQLite installations are detected automatically and continue using `%LocalAppData%\LedgerNest\ledgernest.db`.

For MySQL or SQL Server, give every client the same connection string. LedgerNest creates its schema on the first connection. Use a database account with schema create/read/write permissions.

Invoice, quotation, and receipt numbers are reserved through the shared `document_sequences` table using serializable transactions. A unique `(Type, InvoiceNumber)` index prevents duplicate numbers when clients save at the same time.
