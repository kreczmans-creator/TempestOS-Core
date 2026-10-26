"""Tests of scripts/generate-verification-snapshot.py's reader of the known-defects document (Addendum C, C-4).

The C# twin is tests/Tempest.Calc.Tests.Library/Dashboard/KnownDefectsTableTests.cs; both readers must accept the same bulk lines.
Run: python3 -m unittest discover -s tests/scripts
"""
import importlib.util
import pathlib
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
spec = importlib.util.spec_from_file_location("generate_snapshot", ROOT / "scripts" / "generate-verification-snapshot.py")
gen = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gen)

HEADER = "### Medium (4), bulk classification\n\n| ID | Decision | Evidence |\n|---|---|---|\n"
SUMMARY_HEAD = "| Severity | Fixed | Owner decision | Still open | Not a defect | Total |\n|---|---|---|---|---|---|\n"
TABLE = "| a.one-01 | **Fixed (abc)** | x |\n| a.two-02 | **Still open (partly fixed)** | y |\n"


def read(bulk, summary="| Medium | 1 | 0 | 3 | 0 | 4 |", table=TABLE):
    text = f"{SUMMARY_HEAD}{summary}\n\n{HEADER}{table}\n{bulk}\n"
    with tempfile.TemporaryDirectory() as folder:
        path = pathlib.Path(folder) / "defects.md"
        path.write_text(text, encoding="utf-8")
        return gen.read_defects(path)


class BulkLines(unittest.TestCase):
    def test_code_unchanged_line(self):
        rows = read("Still open, code unchanged (2): a.three-03, a.four-04.")
        self.assertEqual(["a.four-04", "a.one-01", "a.three-03", "a.two-02"], sorted(rows))
        self.assertEqual("fixed", rows["a.one-01"]["status"])
        self.assertTrue(all(rows[i]["status"] == "open" and rows[i]["severity"] == "Medium" for i in ("a.two-02", "a.three-03", "a.four-04")))

    def test_listed_for_the_owner_line_drops_the_note_even_with_commas_in_it(self):
        rows = read("Still open, listed for the owner (2): a.three-03 (a supply-side drop, a relief margin; a cycle), a.four-04 (note).")
        self.assertEqual(["a.four-04", "a.one-01", "a.three-03", "a.two-02"], sorted(rows))

    def test_backticked_ids(self):
        rows = read("Still open, code unchanged (2): `a.three-03`, `a.four-04` (a note).")
        self.assertEqual(["a.four-04", "a.one-01", "a.three-03", "a.two-02"], sorted(rows))

    def test_partly_fixed_is_open_not_fixed(self):
        table = "| a.one-01 | **Partly fixed (abc)** | x |\n| a.two-02 | **Still open (partly fixed)** | y |\n"
        rows = read("Still open, code unchanged (2): a.three-03, a.four-04.", "| Medium | 0 | 0 | 4 | 0 | 4 |", table)
        self.assertTrue(all(r["status"] == "open" for r in rows.values()))

    def test_count_that_does_not_match_is_refused(self):
        with self.assertRaises(SystemExit) as caught:
            read("Still open, listed for the owner (3): a.three-03 (note).")
        self.assertIn("says 3 ids and lists 1", str(caught.exception))

    def test_rows_that_do_not_add_up_to_the_summary_are_refused(self):
        with self.assertRaises(SystemExit) as caught:
            read("Still open, listed for the owner (1): a.three-03 (note).")
        self.assertIn("summary table prints", str(caught.exception))

    def test_id_in_a_table_and_a_bulk_line_is_refused(self):
        with self.assertRaises(SystemExit):
            read("Still open, code unchanged (1): a.two-02.")

    def test_item_that_is_not_an_id_is_refused_not_skipped(self):
        with self.assertRaises(SystemExit) as caught:
            read("Still open, code unchanged (2): a.three-03, (a note only).")
        self.assertIn("does not start with a finding id", str(caught.exception))


class RealDocument(unittest.TestCase):
    def test_the_known_defects_document_in_the_repository_parses_and_adds_up(self):
        rows = gen.read_defects()
        self.assertGreater(len(rows), 100)


if __name__ == "__main__":
    unittest.main()
