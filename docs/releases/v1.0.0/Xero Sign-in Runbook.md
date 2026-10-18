# Xero sign-in runbook (first live authorisation)

For the Product Owner, on the Windows PC that runs TempestOS. About 15
minutes. The product side is already driven to the token exchange
(`WP 21.6P`); nobody has completed the live sign-in yet. This is
`PHYSICAL_REVIEW.md` §7k with the Xero-side setup written out.

## 1. Register the Xero app (once, developer.xero.com)

1. Sign in at <https://developer.xero.com/app/manage> with the Xero
   login that owns the organisation to be invoiced from.
2. **New app** → name `TempestOS` (any name), integration type
   **Web app**, company URL anything valid.
3. Redirect URI, exactly: `http://127.0.0.1:48131/callback/`
   (trailing slash included; `TD-183`, `ADR-0151` addendum).
4. Save. Copy the **Client id**. Generate and copy a **Client secret**
   — TempestOS sends it at the token exchange when one is configured
   (`OAuthAuthoriser.ResolveClientCredentialsAsync`).
5. Scopes are requested by TempestOS, not set in the console:
   `openid profile email accounting.transactions accounting.contacts
   offline_access` (`TempestHost.cs`, the Xero `OAuthProviderProfile`).

## 2. Configure TempestOS

1. Account chip (top right) → **Settings** → *Connector authorisation*.
2. Connector **Xero**; paste Client id and Client secret; **Authorise**.
   Expect **"Saved. Restart TempestOS to use Xero — this session is
   running the Fake connector."** No browser opens yet.
3. Close TempestOS and reopen. Settings → *Connector authorisation*
   must show Xero, the Client id, and **"Not authorised."**

## 3. Sign in

1. **Authorise**. Status: **"Waiting for you to sign in to Xero in your
   browser (up to 5 minutes)…"**; the default browser opens Xero.
2. Sign in, pick the organisation, **Allow**. The browser shows
   *"Authorisation complete. You can close this window and return to
   TempestOS."*; the status reads **"Authorised."**
3. Business → **Invoices**, or Settings → *Refresh accounts reading*:
   contacts, bills or cash position appear from the organisation.

## 4. If it fails

Record the status line verbatim (it names the cause) and paste it in
the project chat. Common ones:

- *redirect-URI mismatch* in the browser → step 1.3 differs by a
  character, or the port was overridden (`Invoicing:OAuth:LoopbackPort`).
- *port in use* → something else holds 48131; set the override key
  above to `0` for a random free port and re-register the URI to match.
- *consent denied* / *timeout* → run step 3 again.
- *Not authorised. not configured* after restart → the Client id did
  not persist; re-enter it (step 2) and restart again.

Tokens are stored DPAPI-protected per Windows user, never in
`Tempest.db` (`SecretStore`); they are not copied by backing up the
database. Then record X1–X5 in `PHYSICAL_REVIEW.md` §7k and the verdict
in `PRODUCT_OWNER_ACCEPTANCE.md`.
