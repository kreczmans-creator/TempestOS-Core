# Attachment Content Storage

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-31` (resolved) · **Decision:** `ADR-0114` ·
**Code:** `Tempest.Core.Persistence.IBinaryPersistenceStore`,
`Tempest.Core.EngineeringDomain.AttachmentContentStore`

## The gap

`IAttachment` had carried `FileName`, `ContentType` and `SizeInBytes`
since `WP 8.2C`. It had never carried the file.

That was disclosed rather than hidden — `TD-31` said so plainly, and every
Attachment record was exactly what it claimed to be: a *description* of a
file. The platform never lied about it. But "attach a document" meant
"write down that a document exists somewhere", and the Document Viewer
(`TD-80`) had nothing to open.

## Where the answer already was

`FCR-0054` had left the question open for two years in three parts:
local filesystem storage, a blob-storage abstraction, or an external
document-management-system integration?

All three are bigger than the answer. `PersistenceStore` **is already a
file store** — it writes one file per record under a configured root. It
is not a database with a string column. Storing bytes in it is not a new
capability; it is the capability it already has, reached through a
different door.

So the decision is a door, not a building:

```
PersistenceStore            <- one class, one instance, one root
  : IPersistenceStore         ReadAsync / WriteAsync        (text)
  : IBinaryPersistenceStore   ReadBytesAsync / WriteBytesAsync (bytes)
```

Both shapes share `GetFilePath`, `LockKey`, `ResolveReadablePath`,
`ExistsWithExactName` and the write-to-temporary-then-rename. They differ
at exactly one point: whether the file is read and written with an
encoding.

> **Why that sharing is the whole point.** Everything that makes the text
> store trustworthy was expensive and none of it is about text:
> reserved-device-name-safe naming and its legacy migration (`TD-59`), the
> per-key lock that folds case-variants onto one lock, the exact-name
> resolution that never returns another key's case-variant record, the
> atomic replacement. A separate byte store would have had to reproduce
> all of it — and would have got some of it wrong, silently, in a
> different place.

## Why not just base64 it

The text store would have held this content with no new contract at all.
It was rejected on arithmetic: a third again the size on disk, and an
encode/decode of the *entire file* on every read and write. A 40 MB
drawing becomes a 53 MB string on the way past.

It also makes fidelity depend on a round-trip the content never asked for.
That matters more than it sounds — see the test payloads below.

## Why not add bytes to `IPersistenceStore`

Twenty test doubles in this repository implement that interface. Adding
byte members would break every one of them to serve callers that will
never store a file. A sibling contract leaves each implementer carrying
only what it actually stores.

## Metadata describes content; it does not assert it

```
IAttachment          FileName, ContentType, SizeInBytes, ContentHash?
                                                          |
IAttachmentContentStore  ------- keyed by attachment Id ---+---> the bytes
```

`AttachContentAsync` **derives** the size and the hash from the bytes it
stores rather than accepting them from the caller, so metadata cannot
describe a file the store does not hold. `AttachDocumentCommand`'s
content overload has no size parameter at all: the size of a file is a
property of its bytes, not a claim you get to make separately from them.

The split earns its keep at startup. `ADR-0113` reads the state of
**every** object during rehydration. That state carries the attachment's
name, type, size and hash — and not one byte of the file. Rehydrating ten
thousand objects therefore costs nothing in attachment content, and the
drawing is read only when someone opens it.

## Two orderings, and only one of them is honest

Content is written **before** metadata.

- Crash between the two, in this order: bytes nothing references.
  Invisible, harmless, reclaimable (`TD-97`).
- Crash between the two, in the other order: an attachment promising
  content that was never stored. A record that lies.

That is a decision, not an accident of how the method happens to read, and
`ADR-0114` records it as one.

## A read that verifies what it returns

```
Available (bytes)  |  Missing  |  Corrupt (no bytes)
```

Three outcomes, not a nullable `byte[]`, and the reasoning is worth
keeping:

- **Damaged bytes are never returned.** Not alongside a flag, not with a
  warning. A flag beside a payload is an invitation to the caller who
  ignores the flag.
- **`Missing` and `Corrupt` are different facts.** "We never held this
  file" and "we held it and this is not it" need different words in the
  viewer, and the distinction has to survive the read to be sayable at
  all.
- **Neither throws.** Both are ordinary answers to a passive read —
  `TD-60`'s discipline: one unreadable record must not cost the caller
  every other one.
- **An unverifiable record says so.** An attachment with no recorded hash
  predates this work and is checked on size alone. It is returned —
  refusing would make every older attachment permanently unreadable — but
  without pretending it was verified.

## The test payloads are the interesting part

A content store that decoded bytes as text, stopped at a NUL, normalised a
line ending, or round-tripped through UTF-8 would round-trip
`new byte[64]` **perfectly** and lose every real file.

So the tests carry the byte patterns that actually catch it: NUL bytes
mid-stream, `0x1A` (the DOS end-of-file some text paths still honour),
bare CR and bare LF, sequences that are not valid UTF-8 at all, and
`0xFF`/`0xFE` leading bytes that look like a byte-order mark. The files
are structurally real — a PDF with its high-byte comment line, a PNG with
real CRCs, the ZIP container every `.docx` actually is, a JPEG, a CSV with
a BOM and a `kg/m³` — plus one blob containing **every one of the 256 byte
values, twice, in both directions**. That last one is the assertion that
covers the whole alphabet rather than sampling it.

> **The transferable lesson.** An empty array is not a test of a byte
> store. It is a test of whether the store returns *something*. Pick
> payloads that can only survive if the thing you are claiming is true.

## The mutation that mattered

Seven mutations were run against the persistence and retrieval path, and
all seven were killed. The instructive one:

> **Record the metadata, but never store the bytes.**

Every Core test still passed. The attachment appeared, with the right file
name, the right size, and a correct hash — computed from bytes that were
never written anywhere. Only the **restart journey** caught it, because
only the restart journey ever asks the store for something it did not just
put in memory.

That is what an acceptance test across process lifetimes is for. A unit
test of an attach method will happily confirm that attaching worked.

## What we did not do, and said so

- Content is stored per attachment, not per distinct file: the same
  document attached three times is stored three times (`TD-95`).
- Reads materialise the whole file; there is no streaming read (`TD-96`).
- Nothing collects orphaned content (`TD-97`).
- No content-type enforcement: the store does not validate that a `.pdf`
  is a PDF. A store that guesses at file types is a store that eventually
  refuses a legitimate one.
- **No viewer.** `TD-31` was the storage boundary. Rendering is `TD-80`.

## Related

`ADR-0114` · `ADR-0053` (the substrate reused) ·
`ADR-0113` (the document/state split this mirrors one level down) ·
`FCR-0054` (implemented here) ·
`34-engineering-object-rehydration.md`
