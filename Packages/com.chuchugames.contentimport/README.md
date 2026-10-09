# Chuchu Games – Content Import

A bulk content pipeline, so that "content is data" in practice.

- `Csv.Parse` / `Csv.ParseRecords` (engine-free): an RFC 4180 reader that handles quoted fields, embedded commas and newlines, doubled quotes and BOMs. Google Sheets and Excel exports load as-is.
- `AssetUpsert.Upsert<T>(folder, id, apply, report)` (editor): creates or updates a ScriptableObject at `folder/id.asset`. Updates keep the GUID, so references survive re-imports, and an asset only counts as "updated" when its serialized data actually changed.
- `AssetUpsert.FindOrphans<T>`: lists assets that are no longer in the source data. They are reported, never deleted.
- `ImportReport`: created / updated / unchanged counts, plus errors and warnings.

Recommended pattern: parse, validate everything, and write nothing unless there are zero errors (see Ghost Hotel's `ContentImporter`).
