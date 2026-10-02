# Xero setup — step by step

For the Product Owner, on the Windows PC that runs TempestOS. Allow 20
minutes. Do every step in order; each one says what you should see. If
what you see differs, stop and copy the exact text into the project
chat.

You need: the Xero login that owns the organisation you invoice from,
and TempestOS installed and opening normally.

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
     `49301` instead.
7. Tick the terms box and click **Create app**.
8. You land on the app's page. Click **Configuration** on the left.
9. Find **Client id**. Click **Copy**, and paste it into Notepad for now.
10. Click **Generate a secret**. Click **Copy** and paste it into Notepad
    under the client id. **Xero only shows the secret once** — if you
    lose it, generate a new one.
11. Click **Save**. Leave the browser open.

You do **not** choose scopes on this page; TempestOS asks for them when
you sign in. For reference, it asks for: `openid profile email
offline_access accounting.invoices accounting.contacts.read
accounting.reports.banksummary.read`. (Xero apps created since 2 March
2026 cannot use the older `accounting.transactions` scope; TempestOS no
longer asks for it.)

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

## Part 3 — Sign in to Xero from TempestOS

22. Open TempestOS again → account chip → **Settings** → **Connector
    authorisation**.
23. Check: connector reads **Xero**, your client id is shown, status
    reads **"Not authorised."**
24. Click **Authorise**.
25. TempestOS reads **"Waiting for you to sign in to Xero in your browser
    (up to 5 minutes)…"** and your web browser opens a Xero page.
26. In the browser, sign in to Xero if asked.
27. Xero lists what TempestOS is asking for. If you have more than one
    organisation, pick the one you invoice from.
28. Click **Allow access**.
29. The browser shows **"Authorisation complete. You can close this
    window and return to TempestOS."** Close that tab.
30. Back in TempestOS the status reads **"Authorised."**

## Part 4 — Check it reads real data

31. Still in Settings, click **Refresh accounts reading**.
32. Expect **"Accounts reading: last at <date time> (Xero)."**
33. Go to **Business → Invoices**. Bills due, repeating bills and the
    cash position now come from your Xero organisation.
34. Record the result against F1–F6 in the runbook.

---

## If something goes wrong

| What you see | What it means | What to do |
|---|---|---|
| Browser: *"Invalid redirect_uri"* / *unauthorized_client* | Step 6 differs by a character, or the build uses the other port | Compare the address bar's `redirect_uri=` with step 6; fix the app's Redirect URI in Xero to match it exactly, Save, retry step 24 |
| Browser: *invalid_scope* | The build still asks for the old `accounting.transactions` scope | You are on a build older than this guide; update TempestOS |
| TempestOS: *port in use* | Another program holds the port | Restart Windows and retry step 24; if it persists, tell the project chat |
| TempestOS: *timed out* or *consent denied* | You took more than 5 minutes, or clicked Cancel | Repeat from step 24 |
| After restart, status reads *not configured* | The id/secret did not save | Repeat Part 2 |
| *Accounts reading: unavailable: …* | Signed in, but a read failed | Copy the full line into the project chat |

Tokens and the secret are stored with Windows' own encryption for your
Windows user; they are never in `Tempest.db` and are not copied by a
database backup. A different Windows user, or a new PC, repeats Parts 2
and 3 (not Part 1).
