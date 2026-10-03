# Xero setup — step by step

For the Product Owner, on the Windows PC that runs TempestOS. Allow 30
minutes. Do every step in order; each one says what you should see. If
what you see differs, stop and copy the exact text into the project
chat.

You need: the Xero login that owns the organisation you invoice from,
and TempestOS installed and opening normally.

**From v0.24.0 TempestOS writes to Xero** (`ADR-0162`): draft invoices,
copies of quotes, draft purchase orders and draft expense bills, each
with its PDF. It never approves an invoice and never emails anyone (D3,
D4). **Connect the Xero Demo Company first** (Part 3, D7): until you
switch on *Allow live organisation* (Part 6), TempestOS refuses to write
to any organisation that is not Xero's Demo Company.

---

## Part 1 — Create the Xero app (once, in a web browser)

1. Open <https://developer.xero.com/app/manage> and sign in with your
   normal Xero login.
2. Click **New app**.
3. **App name**: type `TempestOS`.
4. **Integration type**: choose **Web app**.
5. **Company or application URL**: type `https://tempestos.local` (any
   valid address is accepted; nothing is ever sent there).
6. **Redirect URI**: type exactly this, with nothing before or after:

   `http://127.0.0.1:48131/callback/`

   - `127.0.0.1`, not `localhost`.
   - It ends in a `/` after `callback`.
   - The port is **48131** from the v1.0.0 RC build onwards (`TD-183`).
     Builds up to v0.22 used `49301`; if you are on one of those, type
     `49301` instead. **v0.24.0 test builds** still listen on `49301`
     until `TD-183` merges (v0.23.0 Release Notes, "Known, carried
     forward"); if Xero answers *Invalid redirect_uri*, register the port
     the browser's address bar shows after `redirect_uri=`.
7. Tick the terms box and click **Create app**.
8. You land on the app's page. Click **Configuration** on the left.
9. Find **Client id**. Click **Copy**, and paste it into Notepad for now.
10. Click **Generate a secret**. Click **Copy** and paste it into Notepad
    under the client id. **Xero only shows the secret once** — if you
    lose it, generate a new one.
11. Click **Save**. Leave the browser open.

You do **not** choose scopes on this page; TempestOS asks for them when
you sign in. From v0.24.0 it asks for exactly these six:

| Scope | Why TempestOS needs it |
|---|---|
| `accounting.invoices` | Draft sales invoices and bills, quote copies, draft purchase orders; reading their status back |
| `accounting.contacts` | Finding, linking and **creating** customers and suppliers in Xero (was `accounting.contacts.read` up to v0.23) |
| `accounting.settings.read` | Reading your company details, VAT rates and chart of accounts (read only) |
| `accounting.attachments` | Attaching the TempestOS PDF (or a receipt) to each Xero record |
| `accounting.reports.banksummary.read` | The cash position on the Business dashboard |
| `offline_access` | Staying connected beyond 30 minutes |

It no longer asks for `openid profile email` (nothing used them). It never
asks for payments, bank transactions, journals or payroll. Xero apps
created since 2 March 2026 cannot use the older `accounting.transactions`
scope; TempestOS does not ask for it.

## Part 2 — Give TempestOS the app's details

12. Open TempestOS.
13. Click your account chip, top right → **Settings**.
14. Scroll to **Connector authorisation**.
15. **Connector**: choose **Xero**.
16. **Client id**: paste the client id from Notepad.
17. **Client secret**: paste the secret from Notepad.
18. Click **Authorise**.
19. You should see: **"Saved. Restart TempestOS to use Xero — this
    session is running the Fake connector."** No browser opens yet.
    That is correct.
20. Close TempestOS completely (close the main window).
21. Delete the Notepad text without saving — the id and secret are now
    stored, encrypted for your Windows user only.

## Part 3 — Sign in to the Xero Demo Company first

Before step 22, open Xero in your browser and make sure the **Demo
Company** exists for your login: Xero's organisation menu (top left) →
**My Xero** → **Try the Demo Company** (or open it if it is already
listed). The Demo Company is Xero's own sample organisation; it resets
every 28 days and nothing in it is real. TempestOS is tested against it
before your live organisation is connected (D7).

22. Open TempestOS again → account chip → **Settings** → **Connector
    authorisation**.
23. Check: connector reads **Xero**, your client id is shown, status
    reads **"Not authorised."**
24. Click **Authorise**.
25. TempestOS reads **"Waiting for you to sign in to Xero in your browser
    (up to 5 minutes)…"** and your web browser opens a Xero page.
26. In the browser, sign in to Xero if asked.
27. Xero lists what TempestOS is asking for (the six scopes above). Pick
    **Demo Company (UK)** — not the organisation you invoice from, yet.
28. Click **Allow access**.
29. The browser shows **"Authorisation complete. You can close this
    window and return to TempestOS."** Close that tab.
30. Back in TempestOS the status reads **"Authorised."**

## Part 4 — Check it reads real data

31. Still in Settings, click **Refresh accounts reading**.
32. Expect **"Accounts reading: last at <date time> (Xero)."**
33. Go to **Business → Invoices**. Bills due, repeating bills and the
    cash position now come from the connected organisation.
34. Record the result in the v0.24.0 runbook
    (`docs/releases/v0.24.0/PO Test Runbook.md`, section XS).

## Part 5 — The Xero section in Settings (v0.24.0)

With Xero as the connector, Settings has a **Xero** section.

35. Settings → scroll to **Xero**. Under *Connection* expect
    **"Connected to Xero."**, the line *Required scopes:* listing the six
    scopes above, *Granted:* listing the same six, and *Missing:* reading
    none.
36. Click **Refresh from Xero**. Expect **"Read Demo Company (UK) from
    Xero."**, then *Organisation: Demo Company (UK)* and **"Demo Company:
    yes — Xero's Demo Company; TempestOS may write drafts to it."**
37. *Company details from Xero, read at <time>* lists the Demo Company's
    name, address, VAT and bank details. These now print on quote,
    invoice and timesheet PDFs (D6). Change them in Xero, never in
    TempestOS.
38. Under *Tax types (per VAT rate)* and *Account codes*, leave each
    picker on **Default** for the Demo Company (sales `200`; the
    expense categories use the 400-series accounts, Q10). Under
    *Xero General expenses contact* choose a contact for expenses that
    have no supplier (Q3), or leave **None** (such bills then wait).
39. Leave *Show attached invoice PDFs to the client on Xero's online
    invoice* **off** (Q5: PDFs stay internal to Xero).
40. Click **Save** at the top of Settings.

### Re-authorise (after upgrading from v0.23 or earlier)

A connection made before v0.24.0 lacks `accounting.contacts`,
`accounting.settings.read` and `accounting.attachments`. The Xero section
then reads **"Re-authorise needed."** with *Missing:* naming them, and
every badge reads **Waiting for authorisation**; nothing is lost — the
writes wait.

41. Click **Re-authorise**. The browser opens on Xero's consent page
    (as in steps 25–29); pick the **Demo Company** again and click
    **Allow access**.
42. Back in TempestOS: **"Connected to Xero."**, *Missing:* none, and the
    waiting writes go out on their own within a minute.

## Part 6 — The *Allow live organisation* switch (only after the Demo Company run-through)

The switch sits at the bottom of the Xero section: **Allow live
organisation — let TempestOS write drafts to a Xero organisation that is
not Xero's Demo Company.** It is **off** by default. Off, TempestOS reads
any organisation but writes only to the Demo Company; every write to
another organisation is refused before it leaves your PC, and its badge
reads **Failed** with the reason.

Turn it on only when the runbook's Demo Company sections have passed, the
smoke test (Part 7) is green, **and the project chat confirms review-board
items M1, M2 and M7 are in your build**. Before M1, switching from the
Demo Company to your own organisation can copy records you made while
testing into your live books.

43. Settings → **Xero** → **Re-authorise**, and this time pick **your own
    organisation** on Xero's consent page.
44. **Refresh from Xero**: *Demo Company: no — a live organisation.* and
    **"Writes to <your organisation> are blocked until this switch is on
    and saved."**
45. Tick **Allow live organisation**, read the warning (*TempestOS writes
    draft quotes, invoices, purchase orders and bills into your real Xero
    books.*), then **Save**.
46. Expect **"Allowed: TempestOS writes drafts to <your organisation>,
    your live organisation."** The change is recorded in the audit trail
    (one row per change, naming you and old → new; the action's name is
    confirmed with review-board item m2). Unticking it and saving stops
    writes again at once.

Links between TempestOS records and Xero records belong to one
organisation: nothing linked in the Demo Company is ever used against
your live organisation. Customers and suppliers are linked again (from
**Customers & Suppliers → Link to Xero…**) the first time a document for
them goes to the live organisation. Records you issued or sent while
testing against the Demo Company are not copied to the live organisation
by themselves: each goes there only when you click its **Send to Xero**
(this needs review-board item M1).

## Part 7 — Demo Company smoke test (optional, a developer PC with the repository)

`scripts/xero-demo-smoke.ps1` checks the whole Xero round trip against
the Demo Company in about two minutes, using the tokens TempestOS stored
in Part 3: once with requests the test builds itself, and once through
TempestOS's own code (the contact linker, the quote, purchase order and
bill planners, the invoicing service and the sync engine). It writes
**only** if Xero says the organisation is the Demo Company, and deletes
its drafts afterwards (`-Keep` keeps them).

```powershell
pwsh -NoProfile -File scripts/xero-demo-smoke.ps1 -DataFolder C:\TempestOS-rc24-data
```

It ends with **RESULT: PASSED** and two reports (the second ends
`-production.md`) listing a Xero link for every record it made. Add `-KeyWindow` to also confirm how long Xero
keeps an idempotency key (about seven more minutes).

To run it with a token from a secret store instead of the stored one, pass
`-AccessToken <token> -TenantId <Demo Company tenant id>`. That token is
never refreshed: it is used until 2 minutes before the expiry written in
it (Xero's last 30 minutes), or for 23 minutes when it carries none, so
use a freshly issued one, with at least 14 minutes left for `-KeyWindow`
(both journeys and the probe can run on it, in any order).
`-ClientId` and `-ClientSecret` are ignored with it: nothing is ever sent
to Xero's token endpoint for a supplied token. If it has run out, the first
step says *supplied token expired: supply a fresh one*. Its scopes are read
from the token itself; for a token that does not carry them (or carries an
empty list), add `-Scopes "<granted scopes>"`, or the scope check is
reported *not checked (supplied token)*.

---

## If something goes wrong

| What you see | What it means | What to do |
|---|---|---|
| Browser: *"Invalid redirect_uri"* / *unauthorized_client* | Step 6 differs by a character, or the build uses the other port | Compare the address bar's `redirect_uri=` with step 6; fix the app's Redirect URI in Xero to match it exactly, Save, retry step 24 |
| Browser: *invalid_scope* | The build still asks for the old `accounting.transactions` scope | You are on a build older than this guide; update TempestOS |
| TempestOS: *Authorisation failed. Port 49301 is already in use; free it or configure a different port under 'Invoicing:OAuth:LoopbackPort'.* | Another program holds the port | Restart Windows and retry step 24; if it persists, tell the project chat |
| TempestOS: *No sign-in completed within 5 minutes. Try Authorise again.* or *Authorisation failed. The provider reported: access_denied.* | You took more than 5 minutes, or clicked Cancel on Xero's consent page | Repeat from step 24 |
| After restart, status reads *Not authorised. not configured* | The id/secret did not save | Repeat Part 2 |
| *Accounts reading: unavailable: …* | Signed in, but a read failed | Copy the full line into the project chat |
| Xero section: **Re-authorise needed.** with *Missing:* listed | The connection predates v0.24.0, or a scope was refused at consent | Do steps 41–42 |
| A badge reads **Failed** — *TempestOS blocked the request (D7.live-organisation)* | You are connected to a live organisation and *Allow live organisation* is off | Expected until Part 6. Re-authorise with the Demo Company, or (after the run-through) do Part 6, then **Retry** |
| A badge reads **Failed** — *'<organisation>' is not linked to a Xero contact yet; link or create it under Customers & suppliers.* | The customer or supplier is not linked yet | Customers & Suppliers → the organisation → **Link to Xero…**, then **Retry** (the push also goes out by itself within a minute of the link) |
| A badge reads **Waiting for authorisation** | The token expired or was revoked | Settings → Xero → **Re-authorise** |
| Smoke script: **REFUSED - not the Demo Company** | The stored connection is your live organisation | Re-authorise with the Demo Company (Part 3) and run it again |

Tokens and the secret are stored with Windows' own encryption for your
Windows user; they are never in `Tempest.db` and are not copied by a
database backup. A different Windows user, or a new PC, repeats Parts 2
and 3 (not Part 1).
