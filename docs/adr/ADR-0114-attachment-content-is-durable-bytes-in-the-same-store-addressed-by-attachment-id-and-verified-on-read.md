# ADR-0114: Attachment Content Is Durable Bytes in the Same Store, Addressed by Attachment Id and Verified on Read

## Status

Accepted — `TD-31` (Attachment Content Storage), 2026-08-29. Closes `TD-31` and implements `FCR-0054`. Extends `ADR-0053` (the single persistence substrate) and `ADR-0113` (durable object state) without reopening either.

## Context

`IAttachment` has carried `FileName`, `ContentType` and `SizeInBytes` since `WP 8.2C`, and nothing else. `WP 9.4A` wired the first real consumer — `AttachDocumentCommand` — and disclosed the consequence as `TD-31`:

> "no actual file bytes, no resolvable file path, no URL-fetch or storage capability exists anywhere in this platform. `AttachDocumentCommand` therefore only ever records metadata *about* a file, never the file itself."

That was honest and it was survivable, because every Attachment record was exactly what it claimed to be: a description. It stops being survivable at the Document Viewer (`TD-80`), which has nothing to view. `FCR-0054` left the shape of the answer open — "local filesystem storage? a blob-storage abstraction? an external document-management-system integration?" — and this ADR closes that question.

Three things about the existing platform constrain the answer more than the open question suggests.

**The store is already a file store.** `PersistenceStore` writes one file per record under a configured root. It is not a database with a string column; it is a directory of files that happen to hold text. Storing bytes in it is not a new capability, it is the capability it already has, addressed through a different door.

**Everything that makes that store trustworthy was expensive and is value-agnostic.** Reserved-device-name-safe file naming and its legacy migration (`TD-59`), the per-key lock that folds case-variants onto one lock, the exact-name resolution that never returns a different key's case-variant record, and the write-to-temporary-then-rename that makes an interrupted write leave either the old value or the new one. None of that has anything to do with whether the value is text. A separate byte store would have to reproduce all of it and would get some of it wrong.

**`IPersistenceStore` cannot simply grow byte members.** Twenty test doubles across this repository implement it. Adding bytes to that interface would break every one of them to serve callers that will never store a file.

## Decision

**1. Attachment content is bytes in the same store, through a sibling contract on the same class.**

`IBinaryPersistenceStore` declares `ReadBytesAsync`/`WriteBytesAsync`/`DeleteAsync`, and `PersistenceStore` — the same class, the same instance, the same root — implements it alongside `IPersistenceStore`. The byte path shares `GetFilePath`, `LockKey`, `ResolveReadablePath`, `ExistsWithExactName` and the atomic-rename write outright. Text and bytes differ at exactly one point: whether the file is read and written with an encoding.

There is therefore **no second persistence architecture**: no second root, no second naming scheme, no second locking discipline, and no path to a file outside the store. A stored path would have made the record a promise about someone else's disk, which is the limitation `TD-31` exists to remove, not a way to remove it.

**2. Content is addressed by attachment Id, in its own collection, and never travels with object state.**

`IAttachmentContentStore`/`AttachmentContentStore` writes one record per attachment, keyed by the attachment's own Id, in `EngineeringDomain.AttachmentContent`. This is the same relationship to the substrate that `EngineeringObjectStateStore` has (`ADR-0113`), one level down.

The separation is the point. An engineering object's state — which `ADR-0113` reads for **every** object at startup — carries the attachment's metadata and its content hash, and not one byte of the file. Rehydrating a graph of ten thousand objects therefore costs nothing in attachment content, and a forty-megabyte drawing is read only when someone actually opens it.

**3. Metadata describes content; it does not assert it.**

`IAttachment` gains `ContentHash` — the SHA-256 of the stored bytes, as lowercase hex, or `null` when this platform holds no content. `AttachContentAsync` derives both the size and the hash from the bytes it stores, rather than accepting either from the caller, so metadata cannot describe a file the store does not hold. `AttachAsync` is unchanged and still records metadata alone: an attachment naming a file this platform does not have is a legitimate, permanent state, and is every attachment written before this ADR.

**4. Content is written before metadata.**

A crash between the two leaves bytes nothing references — invisible, harmless, reclaimable. The other order leaves an attachment promising content that was never stored, which is a record that lies. The ordering is a decision, not an accident of how the method reads.

**5. A read verifies what it returns, and distinguishes absent from damaged.**

Every read recomputes the hash and compares the length, and reports one of three outcomes: `Available` with the bytes, `Missing`, or `Corrupt` with no bytes at all.

- Damaged bytes are **not** returned alongside a flag. A flag beside a payload is an invitation to the caller who ignores it.
- `Missing` and `Corrupt` are separate because "we never held this file" and "we held it and this is not it" are different facts, and a viewer that cannot tell them apart will eventually show the second as the first.
- Neither throws. Both are ordinary answers to a passive read, following `TD-60`'s discipline: one unreadable record must not cost the caller every other one.
- An attachment with no recorded hash (written before this ADR) is verified on size alone, and is honest that this is all it can check, rather than reporting an unverifiable record as verified.

## Consequences

**What this buys.** An attached file is now a file this platform holds. It survives process restart and object rehydration, it is verified on the way out, and `TD-80` has something real to render. `FCR-0054`'s open design question is answered with the smallest thing that could work: no blob abstraction, no external DMS integration, no new service to operate.

**Content is not deduplicated.** Two attachments of the same file are two records. Content-addressed storage would deduplicate for free, and was rejected here: it makes deletion a reference-counting problem, and this platform has no attachment deletion path yet to make that trade against. Recorded as `TD-95`.

**Content is read whole, into memory.** `ReadBytesAsync` returns `byte[]`. That is correct for the documents the workflow names and wrong for a very large file, where a stream would avoid materialising it at all. No consumer streams today and the viewer is not built; adding a streaming read later is additive to both contracts. Recorded as `TD-96`.

**Orphaned content is not collected.** Deleting an engineering object does not delete the bytes of its attachments, and a crash between the content write and the metadata write leaves a record nothing references. Both are invisible and cost only disk. Recorded as `TD-97`.

**The state schema grew a field.** `EngineeringObjectAttachmentState.ContentHash` is optional with a `null` default, so a record written before this ADR still deserialises, and reads back as exactly what it means. That the schema carries no version of its own remains `TD-87`; this field was deliberately chosen to be additive so that it does not need one.

**No content-type enforcement.** The store is type-agnostic — bytes are bytes — and does not validate that a `.pdf` is a PDF. Deliberate: a store that guesses at file types is a store that eventually refuses a legitimate one. Type is metadata the caller supplies and the viewer interprets.

## Alternatives Considered

**Base64 into the existing text store.** Would have introduced no new contract at all, and was rejected on cost: a third again the size on disk, and an encode/decode of the entire file on every read and write. It also makes content fidelity depend on a string round-trip it never needed.

**Byte members on `IPersistenceStore`.** Rejected because twenty test doubles implement that interface and none of them stores a file.

**A separate byte store with its own root.** Rejected as the second persistence architecture this work package was explicitly not to introduce, and because it would have had to reproduce `TD-59`'s naming, the locking, the exact-name resolution and the atomic write.

**Storing a path or URL to an external file.** Rejected: this is precisely what `TD-31` describes as the defect. A record pointing at someone else's disk is not durable content.

**Content hash as the storage key (content-addressed).** Rejected for now — see Consequences and `TD-95`.

## Related

`ADR-0053` (the single persistence substrate) · `ADR-0113` (durable object state, whose separation this mirrors) · `TD-31` (closed here) · `FCR-0054` (implemented here) · `TD-59` (the naming this inherits) · `TD-60` (the passive-read discipline this follows) · `TD-80` (the consumer this unblocks) · `TD-87` (the versionless state schema this stayed additive to avoid) · `TD-95`/`TD-96`/`TD-97` (opened here)
