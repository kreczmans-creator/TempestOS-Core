using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>How a smoke run behaves.</summary>
/// <param name="Keep">Keep the drafts in Xero instead of deleting them at the end.</param>
/// <param name="Delay">How the run waits (a 429's <c>Retry-After</c>, the key-retention probe): <see cref="Task.Delay(TimeSpan, CancellationToken)"/> live, the simulator's clock in CI.</param>
/// <param name="Time">The clock that stamps the run's numbers.</param>
/// <param name="SuppliedToken">The token was supplied directly (<see cref="XeroLiveSettings.AccessTokenVariable"/>): with no granted-scope record, S02 is "not checked" rather than failed.</param>
public sealed record XeroDemoSmokeOptions(bool Keep, Func<TimeSpan, CancellationToken, Task> Delay, TimeProvider Time, bool SuppliedToken = false)
{
    /// <summary>
    /// How long after a create the key-retention probe repeats it with the
    /// same <c>Idempotency-Key</c>: past TempestOS's assumed 5-minute
    /// lifetime (<c>IdempotencyKeyLifetime</c>, X5), inside Xero's documented
    /// 6 minutes. A replay here confirms the assumption is safe.
    /// </summary>
    public static readonly TimeSpan KeyProbeInsideWindow = TimeSpan.FromSeconds(330);

    /// <summary>When the probe repeats once more, past Xero's documented 6 minutes, to observe (not require) that the key has been forgotten.</summary>
    public static readonly TimeSpan KeyProbePastWindow = TimeSpan.FromSeconds(420);

    /// <summary>The longest wait honoured for a 429 before the step fails.</summary>
    public static readonly TimeSpan LongestRetryAfter = TimeSpan.FromSeconds(90);
}

/// <summary>
/// The Demo Company smoke journey (`v0.24.0` task X8, design §10.2, D7):
/// connect, read the company, tax rates and accounts and check every scope,
/// link or create the contact <c>TempestOS Smoke</c>, quote
/// <c>DRAFT → SENT → ACCEPTED</c> with its PDF, invoice <c>DRAFT</c> with its
/// PDF (never approved, never emailed), purchase order <c>DRAFT</c> with its
/// PDF, expense bill <c>DRAFT</c> with a receipt, each create repeated under
/// the same <c>Idempotency-Key</c>, read-back, and the design's open items.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never writes unless Xero says this is the Demo Company.</b> The
/// organisation is read first; if <c>IsDemoCompany</c> is not true the run
/// stops with <see cref="XeroDemoSmokeReport.RefusedToWrite"/> and sends no
/// write at all — and the pipeline's own <see cref="XeroWriteSafetyHandler"/>
/// (with <i>Allow the live organisation</i> hard-wired off) would refuse one
/// anyway.
/// </para>
/// <para>
/// <b>One code path, two places.</b> The same journey runs against the real
/// Demo Company (<see cref="XeroLiveSmokeTests"/>, opt-in) and against the
/// in-process simulator on every CI run (<see cref="XeroDemoSmokeJourneyTests"/>),
/// so the smoke test itself is tested and the simulator's <c>Violations</c>
/// log checks every request it makes.
/// </para>
/// </remarks>
internal sealed class XeroDemoSmokeJourney
{
    /// <summary>The smoke contact's name.</summary>
    public const string ContactName = "TempestOS Smoke";

    /// <summary>The smoke contact's <c>ContactNumber</c> (TempestOS's customer code; the natural key the contact is found by).</summary>
    public const string ContactNumber = "TOS-SMOKE";

    /// <summary>
    /// The VAT the smoke bill records on its one line (net 50): deliberately
    /// not the 10.00 Xero would compute at 20%, so S18 can tell the recorded
    /// VAT kept (D5) from VAT recomputed by Xero.
    /// </summary>
    public const decimal RecordedBillVat = 9.99m;

    private static readonly string[] OpenIdScopes = ["openid", "profile", "email"];

    private readonly XeroAccountingApi _api;
    private readonly OAuthAuthoriser _authoriser;
    private readonly IXeroSettingsReader _settings;
    private readonly Func<IReadOnlyList<XeroOutgoingRequest>> _outgoing;
    private readonly XeroDemoSmokeOptions _options;
    private readonly XeroDemoSmokeReport _report = new();
    private readonly List<(string Kind, string Id)> _cleanup = [];

    private string _salesTax = "OUTPUT2";
    private string _purchaseTax = "INPUT2";
    private string? _salesAccount;
    private string? _expenseAccount;
    private string _currency = "GBP";
    private bool _connected;

    /// <summary>Initialises a new instance of the <see cref="XeroDemoSmokeJourney"/> class.</summary>
    /// <param name="api">The typed client, over the production-shaped pipeline (<see cref="XeroLiveConnection"/>).</param>
    /// <param name="authoriser">The authoriser (token, tenant and the granted-scope record).</param>
    /// <param name="settings">The X1 settings reader.</param>
    /// <param name="outgoing">What reached Xero so far (the pipeline's journal).</param>
    /// <param name="options">How the run behaves.</param>
    public XeroDemoSmokeJourney(
        XeroAccountingApi api, OAuthAuthoriser authoriser, IXeroSettingsReader settings, Func<IReadOnlyList<XeroOutgoingRequest>> outgoing, XeroDemoSmokeOptions options)
    {
        _api = api;
        _authoriser = authoriser;
        _settings = settings;
        _outgoing = outgoing;
        _options = options;
        _report.Stamp = options.Time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }

    private string Stamp => _report.Stamp;

    /// <summary>Runs the whole journey; never throws for anything Xero did — a failed step is in the report.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<XeroDemoSmokeReport> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await PrepareAsync(cancellationToken))
                return _report;

            var contactId = await ContactAsync(cancellationToken);
            await QuoteAsync(contactId, cancellationToken);
            await InvoiceAsync(contactId, cancellationToken);
            await PurchaseOrderAsync(contactId, cancellationToken);
            await BillAsync(contactId, cancellationToken);
        }
        catch (SmokeStopped)
        {
            // The failed step is already in the report; later steps depended on it.
        }
        finally
        {
            if (_connected && !_report.RefusedToWrite)
            {
                await CleanUpAsync(cancellationToken);
                CheckJournal();
            }
        }

        return _report;
    }

    /// <summary>
    /// The idempotency-key retention probe (X5 key lifetime, open item F1):
    /// creates a draft bill under key <c>K</c>, repeats the identical create
    /// under <c>K</c> after <see cref="XeroDemoSmokeOptions.KeyProbeInsideWindow"/>
    /// (must replay: same bill, no second one), then once more after
    /// <see cref="XeroDemoSmokeOptions.KeyProbePastWindow"/> (observed only).
    /// Deletes what it made unless kept.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<XeroDemoSmokeReport> RunKeyRetentionProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await PrepareAsync(cancellationToken))
                return _report;

            var contactId = await ContactAsync(cancellationToken);
            var number = $"SMOKE-KEY-{Stamp}";
            var key = $"tos:smoke:{Stamp}:bill-key-probe:create";
            var bill = BillWrite(number, contactId);

            var first = await CallAsync(() => _api.CreateBillAsync(bill, key, cancellationToken), cancellationToken);
            var firstId = Require("K01", "Key probe: draft bill created", first, b => b.InvoiceID, b => $"{number} -> {b.InvoiceID} ({b.Status})");
            _cleanup.Add(("bill", firstId));
            Record("Bill (key probe)", number, firstId, first.Value!.Status ?? "?", BillLink(firstId));

            var started = _options.Time.GetUtcNow();
            await _options.Delay(XeroDemoSmokeOptions.KeyProbeInsideWindow, cancellationToken);
            var inside = await CallAsync(() => _api.CreateBillAsync(bill, key, cancellationToken), cancellationToken);
            var insideElapsed = _options.Time.GetUtcNow() - started;
            var replayedInside = inside.Outcome == ConnectorOutcome.Ok && inside.Value!.InvoiceID == firstId;
            Step("K02", "Key probe: same key after the 5-minute window still replays", replayedInside,
                $"after {Minutes(insideElapsed)}: {Describe(inside, b => b.InvoiceID)}");

            await _options.Delay(XeroDemoSmokeOptions.KeyProbePastWindow - XeroDemoSmokeOptions.KeyProbeInsideWindow, cancellationToken);
            var past = await CallAsync(() => _api.CreateBillAsync(bill, key, cancellationToken), cancellationToken);
            var pastElapsed = _options.Time.GetUtcNow() - started;
            var pastId = past.Outcome == ConnectorOutcome.Ok ? past.Value!.InvoiceID : null;
            if (pastId is not null && pastId != firstId)
            {
                _cleanup.Add(("bill", pastId));
                Record("Bill (key probe, second record)", number, pastId, past.Value!.Status ?? "?", BillLink(pastId));
            }

            var pastObserved = pastId is null
                ? $"after {Minutes(pastElapsed)}: {Describe(past, b => b.InvoiceID)}"
                : pastId == firstId
                    ? $"after {Minutes(pastElapsed)}: still replayed (Xero kept the key longer than documented)"
                    : $"after {Minutes(pastElapsed)}: processed as a new request (a second bill {pastId}) - Xero forgot the key";

            _report.Add(new XeroSmokeFinding(
                "F1",
                "Xero keeps an Idempotency-Key about 6 minutes; TempestOS assumes only 5 (IdempotencyKeyLifetime) and never re-sends a create later than that",
                $"after {Minutes(insideElapsed)}: {(replayedInside ? "replayed the first bill" : "did NOT replay")}; {pastObserved}",
                replayedInside ? XeroDemoSmokeReport.Confirmed : XeroDemoSmokeReport.Differs));
        }
        catch (SmokeStopped)
        {
            // Recorded.
        }
        finally
        {
            if (_connected && !_report.RefusedToWrite)
            {
                await CleanUpAsync(cancellationToken);
                CheckJournal();
            }
        }

        return _report;
    }

    // ------------------------------------------------------------------ steps

    /// <summary>S01–S06: connect, scopes, company/tax/accounts, the Demo Company guard, bank summary, codes. False when the run must not write.</summary>
    private async Task<bool> PrepareAsync(CancellationToken cancellationToken)
    {
        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken);
        Step("S01", "Connected: a current access token and a connected organisation", access.Outcome == AccessTokenOutcome.Ok && !string.IsNullOrEmpty(access.TenantId),
            ConnectionDetail(access));
        if (access.Outcome != AccessTokenOutcome.Ok || string.IsNullOrEmpty(access.TenantId))
            throw new SmokeStopped();

        _connected = true;

        var granted = await _authoriser.ReadGrantedScopesAsync(cancellationToken);
        var missing = granted is null ? [.. XeroScopes.Required] : XeroScopes.Required.Where(s => !granted.Contains(s, StringComparer.Ordinal)).ToList();
        if (granted is null && _options.SuppliedToken)
        {
            // An opaque supplied token with no TEMPEST_XERO_SCOPES: nothing
            // says what it was granted, so S02 cannot judge it. Every later
            // read and write still proves the scopes it uses.
            Step("S02", "Granted scopes include every scope TempestOS requires", true,
                "not checked (supplied token): it carries no scope claim and no -Scopes were given; the steps below exercise each scope");
        }
        else
        {
            Step("S02", "Granted scopes include every scope TempestOS requires", missing.Count == 0,
                granted is null
                    ? "no granted-scope record (tokens from before v0.24.0): re-authorise in Settings"
                    : missing.Count == 0 ? $"granted: {string.Join(' ', granted)}" : $"missing: {string.Join(' ', missing)} - re-authorise in Settings");
        }

        var refresh = await _settings.RefreshAsync(cancellationToken);
        var reading = refresh.Outcome == ConnectorOutcome.Ok ? refresh.Value : null;
        Step("S03", "Read the organisation, tax rates and chart of accounts (accounting.settings.read)", reading is not null,
            reading is null
                ? $"{refresh.Outcome}: {refresh.Reason}"
                : $"{reading.Organisation.Name}; {reading.TaxRates.Count} tax rates; {reading.Accounts.Count} accounts; base currency {reading.Organisation.BaseCurrency}");
        if (reading is null)
            throw new SmokeStopped();

        _report.OrganisationName = reading.Organisation.Name;
        if (!reading.Organisation.IsDemoCompany)
        {
            _report.RefusedToWrite = true;
            Step("S04", "Xero reports IsDemoCompany (D7): the run writes only to the Demo Company", false,
                $"'{reading.Organisation.Name}' is not the Demo Company - nothing was written. Connect the Demo Company and run again.");
            return false;
        }

        Step("S04", "Xero reports IsDemoCompany (D7): the run writes only to the Demo Company", true, $"'{reading.Organisation.Name}' is the Demo Company");

        // The connection (tenant resolution through /connections at sign-in)
        // and every call above worked with this grant: Q9 is settled by
        // whether openid/profile/email are in it.
        if (granted is not null)
        {
            var openId = OpenIdScopes.Where(s => granted.Contains(s, StringComparer.Ordinal)).ToList();
            _report.Add(new XeroSmokeFinding(
                "F3",
                "Q9: TempestOS no longer requests openid profile email; connecting (/connections) and every call work without them",
                openId.Count == 0 ? "connected and read the organisation with a grant that has none of openid/profile/email" : $"the grant still carries {string.Join(' ', openId)} (an older authorisation): re-authorise and run again to confirm",
                openId.Count == 0 ? XeroDemoSmokeReport.Confirmed : XeroDemoSmokeReport.NotRun));
        }

        var bank = await CallAsync(() => _api.GetAsync<JsonElement>("Reports/BankSummary", cancellationToken: cancellationToken), cancellationToken);
        Step("S05", "Read the Bank Summary report (accounting.reports.banksummary.read)", bank.Outcome == ConnectorOutcome.Ok, Describe(bank, _ => "read"));

        _currency = string.IsNullOrWhiteSpace(reading.Organisation.BaseCurrency) ? "GBP" : reading.Organisation.BaseCurrency;
        _salesTax = PickTax(reading, "OUTPUT2", revenue: true) ?? _salesTax;
        _purchaseTax = PickTax(reading, "INPUT2", revenue: false) ?? _purchaseTax;
        _salesAccount = PickAccount(reading, ["200"], "REVENUE");
        _expenseAccount = PickAccount(reading, ["429", "493", "400"], "EXPENSE");
        Step("S06", "Tax types and account codes to use exist in Xero (Q10)", _salesAccount is not null && _expenseAccount is not null,
            $"sales {_salesAccount ?? "(none)"} with {_salesTax}; expenses {_expenseAccount ?? "(none)"} with {_purchaseTax}");
        if (_salesAccount is null || _expenseAccount is null)
            throw new SmokeStopped();

        return true;
    }

    /// <summary>S07: link or create the smoke contact (accounting.contacts, X2).</summary>
    private async Task<string> ContactAsync(CancellationToken cancellationToken)
    {
        var found = await CallAsync(() => _api.FindContactsByContactNumberAsync(ContactNumber, cancellationToken: cancellationToken), cancellationToken);
        if (found.Outcome != ConnectorOutcome.Ok)
        {
            Step("S07", "Link or create the contact 'TempestOS Smoke' (accounting.contacts)", false, Describe(found, _ => "?"));
            throw new SmokeStopped();
        }

        var existing = found.Value!.FirstOrDefault(c => string.Equals(c.ContactStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase) && c.ContactID is not null);
        if (existing is not null)
        {
            Step("S07", "Link or create the contact 'TempestOS Smoke' (accounting.contacts)", true, $"linked existing contact {existing.ContactID} (ContactNumber {ContactNumber})");
            Record("Contact", ContactName, existing.ContactID!, existing.ContactStatus ?? "ACTIVE", $"https://go.xero.com/Contacts/View/{existing.ContactID}");
            _report.Add(new XeroSmokeFinding(
                "F4",
                "A contact can be created under accounting.contacts (X2; fixes M21)",
                "not exercised: the smoke contact already existed from an earlier run (it was created then)",
                XeroDemoSmokeReport.NotRun));
            return existing.ContactID!;
        }

        var create = new XeroWireContactCreate(ContactName, ContactNumber, EmailAddress: "smoke@tempestos.invalid");
        var key = $"tos:smoke:{Stamp}:contact:create";
        var created = await CallAsync(() => _api.CreateContactAsync(create, key, cancellationToken), cancellationToken);
        var contactId = Require("S07", "Link or create the contact 'TempestOS Smoke' (accounting.contacts)", created, c => c.ContactID, c => $"created contact {c.ContactID}");
        Record("Contact", ContactName, contactId, created.Value!.ContactStatus ?? "ACTIVE", $"https://go.xero.com/Contacts/View/{contactId}");

        var again = await CallAsync(() => _api.CreateContactAsync(create, key, cancellationToken), cancellationToken);
        Step("S07a", "Repeating the contact create with the same Idempotency-Key makes no second contact", again.Outcome == ConnectorOutcome.Ok && again.Value!.ContactID == contactId,
            Describe(again, c => c.ContactID));

        _report.Add(new XeroSmokeFinding(
            "F4",
            "A contact can be created under accounting.contacts (X2; fixes M21)",
            created.Outcome == ConnectorOutcome.Ok ? $"created {contactId} with the v0.24.0 grant" : "refused",
            XeroDemoSmokeReport.Confirmed));
        return contactId;
    }

    /// <summary>S08–S11: quote DRAFT with PDF, a second PDF under the same name, then SENT and ACCEPTED (D2, Q1).</summary>
    private async Task QuoteAsync(string contactId, CancellationToken cancellationToken)
    {
        var number = $"SMOKE-Q-{Stamp}";
        var today = Today();
        var quote = new XeroWireQuoteWrite(
            number, "R1", "TempestOS smoke quote", "SMOKE", new XeroWireContactRef(contactId), today, Today(30), "Smoke test - not a real offer.", _currency,
            XeroWire.LineAmountTypesExclusive, [new XeroWireLineItem("Smoke test design review", 2m, 150m, _salesAccount, _salesTax)]);
        var key = $"tos:smoke:{Stamp}:quote:create";

        var created = await CallAsync(() => _api.CreateQuoteAsync(quote, key, cancellationToken), cancellationToken);
        var quoteId = Require("S08", "Quote created as DRAFT", created, q => q.Status == "DRAFT" ? q.QuoteID : null, q => $"{number} -> {q.QuoteID} ({q.Status})");
        Record("Quote", number, quoteId, "DRAFT", $"https://go.xero.com/Accounts/Receivable/Quotes/View/{quoteId}");

        var again = await CallAsync(() => _api.CreateQuoteAsync(quote, key, cancellationToken), cancellationToken);
        var byNumber = await CallAsync(() => _api.FindQuotesByNumberAsync(number, cancellationToken), cancellationToken);
        Step("S08a", "Repeating the quote create with the same Idempotency-Key makes no second quote",
            again.Outcome == ConnectorOutcome.Ok && again.Value!.QuoteID == quoteId && byNumber.Outcome == ConnectorOutcome.Ok && byNumber.Value!.Count == 1,
            $"repeat: {Describe(again, q => q.QuoteID)}; quotes numbered {number}: {Count(byNumber)}");

        var fileName = $"{number}.pdf";
        var r1 = Pdf($"{number} R1");
        var upload = await CallAsync(() => _api.UploadAttachmentAsync(XeroAttachableResource.Quotes, quoteId, r1, $"tos:smoke:{Stamp}:quote:pdf:{r1.Sha256[..16]}", cancellationToken: cancellationToken), cancellationToken);
        Step("S09", "Quote PDF attached", upload.Outcome == ConnectorOutcome.Ok, Describe(upload, a => a.FileName));

        // §3: a new revision's PDF goes up under the same name with POST
        // (update-by-name). Does Xero replace it or keep both?
        var r2 = Pdf($"{number} R2");
        var replace = await CallAsync(() => _api.UploadAttachmentAsync(XeroAttachableResource.Quotes, quoteId, r2, $"tos:smoke:{Stamp}:quote:pdf:{r2.Sha256[..16]}", replaceExisting: true, cancellationToken: cancellationToken), cancellationToken);
        var listed = await CallAsync(() => _api.ListAttachmentsAsync(XeroAttachableResource.Quotes, quoteId, cancellationToken), cancellationToken);
        var sameName = listed.Outcome == ConnectorOutcome.Ok ? listed.Value!.Count(a => string.Equals(a.FileName, fileName, StringComparison.OrdinalIgnoreCase)) : -1;
        Step("S09a", "A second PDF under the same name is accepted (a new revision)", replace.Outcome == ConnectorOutcome.Ok && sameName >= 1,
            $"upload: {Describe(replace, a => a.FileName)}; attachments named {fileName}: {sameName}");
        _report.Add(new XeroSmokeFinding(
            "F2",
            "Attachment replace-by-name: POST .../Attachments/{same name} replaces the earlier file (Xero has no attachment delete; if both are kept, the Rn in Reference disambiguates)",
            sameName switch
            {
                1 => "Xero kept one file under that name: replaced",
                > 1 => $"Xero kept {sameName} files under that name: both kept (Rn in Reference tells them apart)",
                _ => $"could not list attachments: {Describe(listed, _ => "?")}",
            },
            sameName == 1 ? XeroDemoSmokeReport.Confirmed : sameName > 1 ? XeroDemoSmokeReport.Differs : XeroDemoSmokeReport.NotRun));

        var held = created.Value!;
        foreach (var (step, status, word) in new[] { ("S10", XeroQuoteWriteStatus.Sent, "SENT"), ("S11", XeroQuoteWriteStatus.Accepted, "ACCEPTED") })
        {
            var update = new XeroWireQuoteStatusUpdate(quoteId, held.QuoteNumber ?? number, new XeroWireContactRef(contactId), today, status);
            var moved = await CallAsync(() => _api.SetQuoteStatusAsync(update, $"tos:smoke:{Stamp}:quote:status:{word}", cancellationToken), cancellationToken);
            Require(step, $"Quote moved to {word} (following TempestOS)", moved, q => q.Status == word ? q.QuoteID : null, q => $"{q.Status}");
        }

        var readBack = await CallAsync(() => _api.GetQuoteAsync(quoteId, cancellationToken), cancellationToken);
        Step("S11a", "Quote read back: ACCEPTED, with its PDF", readBack.Outcome == ConnectorOutcome.Ok && readBack.Value!.Status == "ACCEPTED" && readBack.Value.HasAttachments != false,
            Describe(readBack, q => $"{q.Status}, attachments {q.HasAttachments?.ToString() ?? "?"}"));
        Record("Quote", number, quoteId, readBack.Value?.Status ?? "?", $"https://go.xero.com/Accounts/Receivable/Quotes/View/{quoteId}");
    }

    /// <summary>S12–S14: sales invoice DRAFT with PDF, read back still DRAFT, never approved, never emailed (D3, D4, Q5).</summary>
    private async Task InvoiceAsync(string contactId, CancellationToken cancellationToken)
    {
        var number = $"SMOKE-INV-{Stamp}";
        var invoice = new XeroWireInvoiceWrite(
            Contact: new XeroWireContactRef(contactId), InvoiceNumber: number, Reference: "SMOKE · time & expenses", Date: Today(), DueDate: Today(30),
            CurrencyCode: _currency, LineAmountTypes: XeroWire.LineAmountTypesExclusive,
            LineItems: [new XeroWireLineItem("Smoke test engineering time", 3m, 95m, _salesAccount, _salesTax)]);
        var key = $"tos:smoke:{Stamp}:invoice:create";

        var created = await CallAsync(() => _api.CreateSalesInvoiceDraftAsync(invoice, key, cancellationToken), cancellationToken);
        var invoiceId = Require("S12", "Invoice created as DRAFT (D3)", created, i => i.Status == "DRAFT" ? i.InvoiceID : null, i => $"{number} -> {i.InvoiceID} ({i.Status})");
        _cleanup.Add(("invoice", invoiceId));
        Record("Invoice", number, invoiceId, "DRAFT", $"https://go.xero.com/AccountsReceivable/View.aspx?InvoiceID={invoiceId}");

        var again = await CallAsync(() => _api.CreateSalesInvoiceDraftAsync(invoice, key, cancellationToken), cancellationToken);
        var byNumber = await CallAsync(() => _api.FindInvoicesByNumberAsync(number, cancellationToken), cancellationToken);
        Step("S12a", "Repeating the invoice create with the same Idempotency-Key makes no second invoice",
            again.Outcome == ConnectorOutcome.Ok && again.Value!.InvoiceID == invoiceId && byNumber.Outcome == ConnectorOutcome.Ok && byNumber.Value!.Count(i => i.Status != "DELETED") == 1,
            $"repeat: {Describe(again, i => i.InvoiceID)}; invoices numbered {number}: {Count(byNumber)}");

        var pdf = Pdf(number);
        var upload = await CallAsync(() => _api.UploadAttachmentAsync(XeroAttachableResource.Invoices, invoiceId, pdf, $"tos:smoke:{Stamp}:invoice:pdf:{pdf.Sha256[..16]}", includeOnline: false, cancellationToken: cancellationToken), cancellationToken);
        Step("S13", "Invoice PDF attached, not shown online (Q5)", upload.Outcome == ConnectorOutcome.Ok && upload.Value!.IncludeOnline != true,
            Describe(upload, a => $"{a.FileName}, IncludeOnline {a.IncludeOnline?.ToString() ?? "?"}"));

        var readBack = await CallAsync(() => _api.GetInvoiceAsync(invoiceId, cancellationToken), cancellationToken);
        var raw = await CallAsync(() => _api.GetAsync<JsonElement>($"Invoices/{invoiceId}", cancellationToken: cancellationToken), cancellationToken);
        var sentToContact = raw.Outcome == ConnectorOutcome.Ok ? ReadSentToContact(raw.Value) : null;
        Step("S14", "Invoice read back: still DRAFT, never AUTHORISED, never sent to the contact (D3, D4)",
            readBack.Outcome == ConnectorOutcome.Ok && readBack.Value!.Status == "DRAFT" && sentToContact != true,
            $"{Describe(readBack, i => i.Status)}; SentToContact {sentToContact?.ToString() ?? "(absent)"}");
        Record("Invoice", number, invoiceId, readBack.Value?.Status ?? "?", $"https://go.xero.com/AccountsReceivable/View.aspx?InvoiceID={invoiceId}");
    }

    /// <summary>S15–S16: purchase order DRAFT with PDF (D5, Q2).</summary>
    private async Task PurchaseOrderAsync(string contactId, CancellationToken cancellationToken)
    {
        var number = $"SMOKE-PO-{Stamp}";
        var order = new XeroWirePurchaseOrderWrite(
            number, "SMOKE", new XeroWireContactRef(contactId), Today(), Today(14), _currency, XeroWire.LineAmountTypesExclusive,
            [new XeroWireLineItem("Smoke test aluminium bar", 4m, 25m, _expenseAccount, _purchaseTax)]);
        var key = $"tos:smoke:{Stamp}:po:create";

        var created = await CallAsync(() => _api.CreatePurchaseOrderAsync(order, key, cancellationToken), cancellationToken);
        var orderId = Require("S15", "Purchase order created as DRAFT (Q2)", created, p => p.Status == "DRAFT" ? p.PurchaseOrderID : null, p => $"{number} -> {p.PurchaseOrderID} ({p.Status})");
        _cleanup.Add(("po", orderId));
        Record("Purchase order", number, orderId, "DRAFT", $"https://go.xero.com/Accounts/Payable/PurchaseOrders/View/{orderId}");

        var again = await CallAsync(() => _api.CreatePurchaseOrderAsync(order, key, cancellationToken), cancellationToken);
        var byNumber = await CallAsync(() => _api.FindPurchaseOrdersByNumberAsync(number, cancellationToken), cancellationToken);
        Step("S15a", "Repeating the purchase order create with the same Idempotency-Key makes no second order",
            again.Outcome == ConnectorOutcome.Ok && again.Value!.PurchaseOrderID == orderId && byNumber.Outcome == ConnectorOutcome.Ok && byNumber.Value!.Count(p => p.Status != "DELETED") == 1,
            $"repeat: {Describe(again, p => p.PurchaseOrderID)}; orders numbered {number}: {Count(byNumber)}");

        var pdf = Pdf(number);
        var upload = await CallAsync(() => _api.UploadAttachmentAsync(XeroAttachableResource.PurchaseOrders, orderId, pdf, $"tos:smoke:{Stamp}:po:pdf:{pdf.Sha256[..16]}", cancellationToken: cancellationToken), cancellationToken);
        var readBack = await CallAsync(() => _api.GetPurchaseOrderAsync(orderId, cancellationToken), cancellationToken);
        Step("S16", "Purchase order PDF attached; read back still DRAFT", upload.Outcome == ConnectorOutcome.Ok && readBack.Outcome == ConnectorOutcome.Ok && readBack.Value!.Status == "DRAFT",
            $"upload: {Describe(upload, a => a.FileName)}; read back: {Describe(readBack, p => p.Status)}");
        Record("Purchase order", number, orderId, readBack.Value?.Status ?? "?", $"https://go.xero.com/Accounts/Payable/PurchaseOrders/View/{orderId}");
    }

    /// <summary>S17–S18: expense bill (ACCPAY) DRAFT with a receipt (D5, Q3, Q4).</summary>
    private async Task BillAsync(string contactId, CancellationToken cancellationToken)
    {
        var number = $"SMOKE-EXP-{Stamp}";
        var bill = BillWrite(number, contactId);
        var key = $"tos:smoke:{Stamp}:bill:create";

        var created = await CallAsync(() => _api.CreateBillAsync(bill, key, cancellationToken), cancellationToken);
        var billId = Require("S17", "Expense bill created as DRAFT (ACCPAY)", created, b => b.Status == "DRAFT" ? b.InvoiceID : null, b => $"{number} -> {b.InvoiceID} ({b.Status})");
        _cleanup.Add(("bill", billId));
        Record("Bill", number, billId, "DRAFT", BillLink(billId));

        var again = await CallAsync(() => _api.CreateBillAsync(bill, key, cancellationToken), cancellationToken);
        var byNumber = await CallAsync(() => _api.FindBillsAsync(number, contactId, cancellationToken), cancellationToken);
        Step("S17a", "Repeating the bill create with the same Idempotency-Key makes no second bill",
            again.Outcome == ConnectorOutcome.Ok && again.Value!.InvoiceID == billId && byNumber.Outcome == ConnectorOutcome.Ok && byNumber.Value!.Count(b => b.Status != "DELETED") == 1,
            $"repeat: {Describe(again, b => b.InvoiceID)}; bills numbered {number}: {Count(byNumber)}");

        var receipt = Receipt(number);
        var upload = await CallAsync(() => _api.UploadAttachmentAsync(XeroAttachableResource.Invoices, billId, receipt, $"tos:smoke:{Stamp}:bill:receipt:{receipt.Sha256[..16]}", cancellationToken: cancellationToken), cancellationToken);
        var readBack = await CallAsync(() => _api.GetBillAsync(billId, cancellationToken), cancellationToken);
        Step("S18", "Receipt attached to the bill; read back still DRAFT with the recorded VAT",
            upload.Outcome == ConnectorOutcome.Ok && readBack.Outcome == ConnectorOutcome.Ok && readBack.Value!.Status == "DRAFT" && readBack.Value.TotalTax == RecordedBillVat,
            $"upload: {Describe(upload, a => a.FileName)}; read back: {Describe(readBack, b => $"{b.Status}, tax {b.TotalTax?.ToString(CultureInfo.InvariantCulture) ?? "?"} (recorded {RecordedBillVat.ToString(CultureInfo.InvariantCulture)}{(b.TotalTax == RecordedBillVat ? ", kept" : ", NOT kept: Xero recomputed it")})")}");
        Record("Bill", number, billId, readBack.Value?.Status ?? "?", BillLink(billId));
    }

    /// <summary>S19: delete the drafts the run made (unless kept). The quote stays: TempestOS never deletes a quote.</summary>
    private async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        if (_cleanup.Count == 0)
            return;

        if (_options.Keep)
        {
            Step("S19", "Drafts kept for inspection (-Keep)", true, $"{_cleanup.Count} draft(s) left in the Demo Company; delete them in Xero when done");
            return;
        }

        var problems = new List<string>();
        foreach (var (kind, id) in _cleanup)
        {
            var key = $"tos:smoke:{Stamp}:{kind}:{id}:delete";
            var (outcome, status, reason) = kind switch
            {
                "invoice" => Shape(await CallAsync(() => _api.DeleteInvoiceDraftAsync(id, key, cancellationToken), cancellationToken), i => i.Status),
                "po" => Shape(await CallAsync(() => _api.DeletePurchaseOrderAsync(id, key, cancellationToken), cancellationToken), p => p.Status),
                _ => Shape(await CallAsync(() => _api.DeleteBillAsync(id, key, cancellationToken), cancellationToken), b => b.Status),
            };

            if (outcome != ConnectorOutcome.Ok || status != "DELETED")
                problems.Add($"{kind} {id}: {outcome} {status} {reason}".TrimEnd());
            else
                UpdateRecordStatus(id, "DELETED");
        }

        Step("S19", "The run's drafts deleted (the accepted quote is left; delete it in Xero if wanted)", problems.Count == 0,
            problems.Count == 0 ? $"{_cleanup.Count} draft(s) deleted" : string.Join("; ", problems));
    }

    /// <summary>S20: nothing that left the machine approved, emailed or wrote outside the allow-list (D3, D4, §7.1).</summary>
    private void CheckJournal()
    {
        var problems = new List<string>();
        foreach (var request in _outgoing())
        {
            var path = request.PathAndQuery;
            if (path.Contains("/Email", StringComparison.OrdinalIgnoreCase))
                problems.Add($"{request.Method} {path}: an email endpoint");
            if (request.Method == "GET")
                continue;

            var body = request.JsonBody ?? string.Empty;
            foreach (var word in new[] { "\"AUTHORISED\"", "\"SUBMITTED\"", "\"PAID\"", "\"VOIDED\"" })
            {
                if (body.Contains(word, StringComparison.Ordinal))
                    problems.Add($"{request.Method} {path}: status {word}");
            }

            if (body.Contains("\"SentToContact\":true", StringComparison.OrdinalIgnoreCase))
                problems.Add($"{request.Method} {path}: SentToContact");

            var resource = ResourceOf(path);
            if (resource is not ("Contacts" or "Quotes" or "Invoices" or "PurchaseOrders"))
                problems.Add($"{request.Method} {path}: outside the write allow-list");
        }

        Step("S20", "Nothing that reached Xero approved, emailed or wrote outside the allow-list (D3, D4)", problems.Count == 0,
            problems.Count == 0 ? $"{_outgoing().Count} request(s) checked" : string.Join("; ", problems));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Calls Xero, honouring a 429's <c>Retry-After</c> (the same request, the same key) up to three times.</summary>
    private async Task<XeroApiResult<T>> CallAsync<T>(Func<Task<XeroApiResult<T>>> call, CancellationToken cancellationToken)
    {
        var result = await call();
        for (var attempt = 0; attempt < 3 && result.Outcome == ConnectorOutcome.Unavailable && result.HttpStatus == 429; attempt++)
        {
            var wait = (result.RetryAfter ?? TimeSpan.FromSeconds(60)) + TimeSpan.FromSeconds(1);
            if (wait > XeroDemoSmokeOptions.LongestRetryAfter)
                break;

            await _options.Delay(wait, cancellationToken);
            result = await call();
        }

        return result;
    }

    private string Require<T>(string id, string title, XeroApiResult<T> result, Func<T, string?> pick, Func<T, string> describe)
    {
        var value = result.Outcome == ConnectorOutcome.Ok ? pick(result.Value!) : null;
        Step(id, title, value is not null, result.Outcome == ConnectorOutcome.Ok ? describe(result.Value!) : Describe(result, _ => "?"));
        return value ?? throw new SmokeStopped();
    }

    /// <summary>S01's detail: what is wrong with the connection and what the operator does about it (a supplied token is never refreshed, so the advice is to supply a fresh one, never to connect).</summary>
    private string ConnectionDetail(AccessTokenResult access)
    {
        if (access.Outcome == AccessTokenOutcome.Ok)
        {
            if (!string.IsNullOrEmpty(access.TenantId))
                return "token current; tenant connected";

            return _options.SuppliedToken
                ? "token current but no tenant: run the script with -TenantId (the Demo Company's tenant id) beside -AccessToken"
                : "token current but no connected organisation: connect the Demo Company in Settings -> Xero, or run the script with -Connect";
        }

        var outcome = $"{access.Outcome}{(access.Reason is null ? string.Empty : $" ({access.Reason})")}";
        return _options.SuppliedToken
            ? $"{outcome}: supplied token expired: supply a fresh one with -AccessToken (a supplied token is never refreshed; it must have more than 2 minutes left)"
            : $"{outcome}: connect the Demo Company in Settings -> Xero, or run the script with -Connect (and -ClientId when the app has none stored)";
    }

    private void Step(string id, string title, bool passed, string detail) => _report.Add(new XeroSmokeStep(id, title, passed, detail));

    private void Record(string kind, string number, string id, string status, string link) => _report.Add(new XeroSmokeRecord(kind, number, id, status, link));

    private void UpdateRecordStatus(string id, string status)
    {
        if (_report.Records.FirstOrDefault(r => r.XeroId == id) is { } record)
            _report.Add(record with { Status = status });
    }

    private XeroWireBillWrite BillWrite(string number, string contactId) => new(
        number, new XeroWireContactRef(contactId), Today(), _currency, XeroWire.LineAmountTypesExclusive,
        [new XeroWireLineItem("Smoke test train fare", 1m, 50m, _expenseAccount, _purchaseTax, TaxAmount: RecordedBillVat)]);

    private string Today(int addDays = 0) => DateOnly.FromDateTime(_options.Time.GetUtcNow().UtcDateTime).AddDays(addDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string BillLink(string id) => $"https://go.xero.com/AccountsPayable/View.aspx?InvoiceID={id}";

    private static string Minutes(TimeSpan elapsed) => elapsed.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min";

    private static string Describe<T>(XeroApiResult<T> result, Func<T, string?> ok) =>
        result.Outcome == ConnectorOutcome.Ok
            ? ok(result.Value!) ?? "ok"
            : $"{result.Outcome} {result.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}: {result.Reason}".Replace("  ", " ", StringComparison.Ordinal);

    private static string Count<T>(XeroApiResult<IReadOnlyList<T>> result) =>
        result.Outcome == ConnectorOutcome.Ok ? result.Value!.Count.ToString(CultureInfo.InvariantCulture) : Describe(result, _ => "?");

    private static (ConnectorOutcome Outcome, string? Status, string? Reason) Shape<T>(XeroApiResult<T> result, Func<T, string?> status) =>
        (result.Outcome, result.Outcome == ConnectorOutcome.Ok ? status(result.Value!) : null, result.Reason);

    private static string? PickTax(XeroSettingsReading reading, string preferred, bool revenue)
    {
        bool Usable(XeroTaxRate rate) => string.Equals(rate.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && (revenue ? rate.CanApplyToRevenue : rate.CanApplyToExpenses);

        return reading.TaxRates.FirstOrDefault(r => r.TaxType == preferred && Usable(r))?.TaxType;
    }

    private static string? PickAccount(XeroSettingsReading reading, IReadOnlyList<string> preferred, string accountClass)
    {
        var active = reading.Accounts.Where(a => a.Code is not null && string.Equals(a.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var code in preferred)
        {
            if (active.FirstOrDefault(a => a.Code == code) is { } match)
                return match.Code;
        }

        return active.FirstOrDefault(a => string.Equals(a.Class, accountClass, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private static bool? ReadSentToContact(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Invoices", out var invoices) || invoices.ValueKind != JsonValueKind.Array || invoices.GetArrayLength() == 0)
            return null;

        var invoice = invoices[0];
        return invoice.TryGetProperty("SentToContact", out var sent) && sent.ValueKind is JsonValueKind.True or JsonValueKind.False ? sent.GetBoolean() : null;
    }

    private static string? ResourceOf(string pathAndQuery)
    {
        var path = pathAndQuery.Split('?')[0];
        const string root = "/api.xro/2.0/";
        var start = path.IndexOf(root, StringComparison.OrdinalIgnoreCase);
        var relative = start >= 0 ? path[(start + root.Length)..] : path.TrimStart('/');
        return relative.Split('/')[0];
    }

    /// <summary>A small, valid one-page PDF naming <paramref name="title"/> — what TempestOS attaches is a rendered PDF; the content does not matter to Xero.</summary>
    /// <param name="title">The text on the page and the file's base name.</param>
    internal static XeroDocumentFile Pdf(string title)
    {
        var fileName = $"{title.Split(' ')[0]}.pdf";
        var text = title.Replace("(", string.Empty, StringComparison.Ordinal).Replace(")", string.Empty, StringComparison.Ordinal);
        var stream = $"BT /F1 18 Tf 72 720 Td (TempestOS smoke: {text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        var bytes = Encoding.ASCII.GetBytes(pdf.ToString());
        return new XeroDocumentFile(fileName, "application/pdf", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>A 1×1 PNG standing in for a photographed receipt.</summary>
    /// <param name="number">The bill number (part of the file name).</param>
    internal static XeroDocumentFile Receipt(string number)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        return new XeroDocumentFile($"receipt-{number}.png", "image/png", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>Stops the journey after a failed step that later steps depend on.</summary>
    private sealed class SmokeStopped : Exception
    {
    }
}
