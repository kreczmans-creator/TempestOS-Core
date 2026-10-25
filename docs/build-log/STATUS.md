# STATUS — 2026-10-24 (Addendum A complete)
Addendum A: 13/13 — 12 calculators (M4, M5, M6, S4, S5, S6, B4, B5, B6, E4, E5, E6), L1 result linking with table-cell links, reference records with the licensed-source export lock, five linked chains with issued pack worked examples, Desktop saved-calculations and packs panels, Security and every colour team, owner test pack and handover
Addendum B: 2/19 — B1 orientation; B-0 foundations merged (Fluid Power discipline ADR-0170, W3 material library, gas-law kernel, tolerance-stack engine; Opus review 6 Medium + 9 Low fixed); B-1 (M7, S7, B7, E7, F2) built, Opus-reviewed (2 High, 12 Medium fixed), in gate; B-2 (F1, F3) in build
Addendum C (Track S): 0/8 — queued after Addendum B (C0); Track P runs in a separate session
Unconfirmed (Addendum B, §B4): S7 `ka-annex-b-class2`, `ka-annex-b-class3-b1`, `ka-annex-a-class2`, `ka-annex-a-class3`, worked example `ub-457x191x98-beam-column` (Annex A/B coefficients from memory); M7 `ka-torsion-en`, `ka-squared-en` (EN 13906 relations from the review); F2 `ka-parker-hy10-4004` (Secondary); E7 `ka-published-3a-10k-1oz`, `-internal` (Secondary); M7 `ka-shigley-10-3` (Secondary, solutions manual). Not pinned (owner copies needed): SCI P364 ex. 3, 4, 11, 12; STAAD UKB 356×171×51; Shigley 10-5, 10-6; a published Glaser example; NFPA accumulator example; ISO 13788 and IPC-2221 clause numbers.
Owner test pack: docs/testing/v0.26.0-owner-test-pack.md (handover: docs/handover/v0.26.0-handover.md, Addendum A section)
Unconfirmed (13, DA-11): M5 `ka-skf-6206`; S5 `ka-designers-guide`, worked example `office-floor-beam-uk-na`; B5 `ka-incropera-11-1`; E6 `ka-peukert`, `ka-psu-derating`; E5 `ka-wiring-matters-b32`, `ka-schneider-k`; M4 `ka-shigley-7-1`; S4 `ka-p363-m20-88`, `ka-p358-fin-plate`; base M1, S3, B2 cases; S6 worked example `portal-column-base-uc254`. Not pinned (owner copies needed): see the test pack §4.
§A9 gates:
- [x] release/v0.26.0 builds (0 warnings); all automated tests pass — Linux: Engine 384, Sheets 111, Calculators 869, Library 168 (+1 skip), Core 7,230 (+15 skip), Desktop 1,223, 0 failures; Windows CI green on 510f337c (runs 40136611994 push, 40136614956 PR)
- [x] L1 linking and packs complete with tests (ADR-0168 and amendments 1–3; five chain test classes; pack dry run)
- [x] Each addendum calculator meets §A3 (none dropped)
- [x] E4 and E5 both present
- [x] No BS/IET/IEC/CIBSE/MCS/NA table data in source, seeds, fixtures or tests — grep audit (White) and manual audit PASS with notes (DA-25)
- [x] Licensed records proven non-exportable by tests (record export refused; CSV/JSON withhold and block re-import; issued sheets withhold licensed table rows)
- [x] Security review and all ten colour-team reports have an Addendum A section; no open Critical/High (Purple verified)
- [x] Owner test pack and handover written
No tag, no release, no merge to main (Addendum A §A0, DA-01). PR #31 stays draft. Version 0.26.0.
Owner steps: Windows real-shell run of the test pack; delete the superseded remote branch v0.26.0/addA-7b-chains; the never-pushed local v0.26.0-rc1 tag; record the SKF catalogue terms (DA-25); confirm DA-24 against BS EN 12828.
Risks: R-22 orphan Draft Evidence after a crash mid-Create (accepted, D-32); residual risks RR-1…RR-12 in the test pack §7.
READY FOR OWNER TESTING — not released, not tagged.
