# Changelog

## Unreleased

### Added
- Admin API endpoint (`POST /api/admin/upload-pdb`) and React UI for uploading `.pdb` files, which invokes `symstore.exe` to persist symbols into the configured store automatically.
- `IPdbIngestionService` with a SymStore-backed implementation that resolves the executable path, executes `symstore add`, and surfaces the command/output for troubleshooting.
- New CrashDumpSettings knobs (`SymStorePath`, `SymbolStoreRoot`, `SymbolStoreProduct`) exposed through the options editor so administrators can manage symbol ingestion end-to-end.
- Public `POST /api/pdb/upload` endpoint plus a dedicated “Upload PDB” workspace tab so regular users can register symbols without entering the admin panel.
