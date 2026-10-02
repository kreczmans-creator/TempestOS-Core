# v0.24.0 Xero — build defaults for the design's open questions

Chief-engineer defaults (2026-10-02) so the build is not blocked. Each
is easy to change and is listed for the Product Owner to confirm.

| Q | Default used by the build |
|---|---|
| Q1 | A quote goes to Xero as **DRAFT** on export and becomes **SENT** only when it is Sent in TempestOS; revisions update it while it is still DRAFT. |
| Q2 | Purchase orders go to Xero as **DRAFT** (same rule as invoices, D3). |
| Q3 | An expense gains an optional supplier (a Customers & Suppliers organisation); with none, the bill goes against one configured "General expenses" contact. |
| Q4 | Bill number: the supplier's invoice number when entered, otherwise `EXP-{expense id}`. |
| Q5 | Attached PDFs are **not** shown on Xero's online invoice (`IncludeOnline` false); the PO can switch it on. |
| Q6 | An expense recorded from a received PO's lines is **not** pushed as a separate bill (the PO uses Xero's "Copy to bill"); the expense records the link. |
| Q7 | When linking an existing Xero contact, TempestOS writes its customer code into `ContactNumber` only when that field is empty. |
| Q8 | Pre-v0.24.0 quotes and invoices are not pushed automatically; each has a "Send to Xero" action. |
| Q9 | Drop `openid profile email`; the Demo Company smoke test confirms the connection still works. |
| Q10 | Account codes are read from Xero; Settings maps the sales account and each expense category, defaulting to the UK Demo Company's codes (200 Sales; 400-series expenses). |
