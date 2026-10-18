using Tempest.Core.Expenses;
using Tempest.Core.Settings;

namespace Tempest.Core.Invoicing.Xero.Settings;

/// <summary>What a Xero account code is used for — which decides the account class it must have.</summary>
public enum XeroAccountPurpose
{
    /// <summary>Sales lines (quotes, sales invoices): an account of class <c>REVENUE</c>.</summary>
    Sales,

    /// <summary>Purchase lines (bills, purchase orders): an account of class <c>EXPENSE</c>.</summary>
    Expense,
}

/// <summary>
/// The Xero account code each TempestOS line posts to (`v0.24.0` X1, D6;
/// design §8 "Account codes", build decision Q10): one sales account for
/// quotes and invoices, and one per <see cref="ExpenseCategory"/> for bills
/// and purchase-order lines — chosen in Settings from the reading's active
/// accounts, and checked against the last reading so a line never carries
/// a code Xero lacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defaults (Q10)</b> are the UK Demo Company's own codes, so the Demo
/// Company works with no set-up: sales <c>200</c> (Sales); Travel and
/// Subsistence <c>493</c> (Travel - National — the Demo Company has no
/// separate subsistence account); Materials and Other <c>429</c> (General
/// Expenses); Subcontract <c>412</c> (Consulting &amp; Accounting). A live
/// organisation's chart differs: a default it lacks Blocks the push with
/// the reason until Settings names one it holds.
/// </para>
/// <para>
/// <b>Checked against the last reading</b> (offline included): the code
/// must exist, be <c>ACTIVE</c>, and be of the purpose's class
/// (<see cref="Check"/>). No reading, or no code chosen, Blocks.
/// </para>
/// </remarks>
public sealed class XeroAccountCodeMap
{
    /// <summary>The Settings key holding the sales account code.</summary>
    public const string SalesSettingKey = "Xero.AccountCode.Sales";

    /// <summary>The prefix of the per-category expense account Settings keys.</summary>
    public const string ExpenseSettingKeyPrefix = "Xero.AccountCode.Expense.";

    /// <summary>The default sales account (Q10): the UK Demo Company's <c>200</c> Sales.</summary>
    public const string DefaultSalesAccountCode = "200";

    private readonly IXeroSettingsReader _reader;
    private readonly ISettingsProvider _settings;
    private int _definitionsEnsured;

    /// <summary>Initialises a new instance of the <see cref="XeroAccountCodeMap"/> class.</summary>
    /// <param name="reader">The X1 settings reader (its cached reading is used; no network).</param>
    /// <param name="settings">Where the chosen account codes are kept.</param>
    public XeroAccountCodeMap(IXeroSettingsReader reader, ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(settings);

        _reader = reader;
        _settings = settings;
    }

    /// <summary>The Settings key holding the account code for <paramref name="category"/> (for example <c>Xero.AccountCode.Expense.Travel</c>).</summary>
    /// <param name="category">The expense category.</param>
    public static string ExpenseSettingKey(ExpenseCategory category) => $"{ExpenseSettingKeyPrefix}{category}";

    /// <summary>The default account code for <paramref name="category"/> (Q10; see the type's remarks).</summary>
    /// <param name="category">The expense category.</param>
    public static string DefaultExpenseAccountCode(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Travel => "493",
        ExpenseCategory.Subsistence => "493",
        ExpenseCategory.Materials => "429",
        ExpenseCategory.Subcontract => "412",
        _ => "429",
    };

    /// <summary>
    /// Registers the sales and every per-category Settings definition (with
    /// the Q10 defaults) unless already registered — idempotent, so the map
    /// and the Settings UI may each call it.
    /// </summary>
    /// <param name="settings">The settings provider.</param>
    public static void EnsureDefinitions(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Register(settings, new SettingDefinition(SalesSettingKey, "Xero sales account code", DefaultSalesAccountCode));
        foreach (var category in Enum.GetValues<ExpenseCategory>())
            Register(settings, new SettingDefinition(ExpenseSettingKey(category), $"Xero account code for {category} expenses", DefaultExpenseAccountCode(category)));
    }

    /// <summary>The sales account code quotes and invoices carry, checked against the cached reading, or why the push is Blocked. No network call.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<XeroCodeResolution> ResolveSalesAsync(CancellationToken cancellationToken = default)
    {
        var code = await ReadChoiceAsync(SalesSettingKey, DefaultSalesAccountCode, cancellationToken).ConfigureAwait(false);
        var reading = await _reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);

        return Check(code, XeroAccountPurpose.Sales, reading);
    }

    /// <summary>The account code a <paramref name="category"/> bill or purchase-order line carries, checked against the cached reading, or why the push is Blocked. No network call.</summary>
    /// <param name="category">The expense category.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<XeroCodeResolution> ResolveExpenseAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(category))
            return XeroCodeResolution.Blocked($"Expense category '{category}' has no Xero account; choose one in Settings.");

        var code = await ReadChoiceAsync(ExpenseSettingKey(category), DefaultExpenseAccountCode(category), cancellationToken).ConfigureAwait(false);
        var reading = await _reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);

        return Check(code, XeroAccountPurpose.Expense, reading);
    }

    /// <summary>
    /// Pure: whether <paramref name="code"/> may be posted to for
    /// <paramref name="purpose"/> according to <paramref name="reading"/> —
    /// it exists (ignoring case), is <c>ACTIVE</c>, and is of the purpose's
    /// class.
    /// </summary>
    /// <param name="code">The chosen account code; blank Blocks.</param>
    /// <param name="purpose">Sales or expense.</param>
    /// <param name="reading">The last reading of Xero; <see langword="null"/> Blocks.</param>
    public static XeroCodeResolution Check(string? code, XeroAccountPurpose purpose, XeroSettingsReading? reading)
    {
        var what = purpose == XeroAccountPurpose.Sales ? "sales" : "expense";

        if (string.IsNullOrWhiteSpace(code))
            return XeroCodeResolution.Blocked($"No Xero {what} account is chosen; choose one in Settings.");

        if (reading is null)
            return XeroCodeResolution.Blocked("Xero's chart of accounts has not been read yet; refresh Xero in Settings.");

        var trimmed = code.Trim();
        // Xero can hold an archived account beside an active one that reuses its code: the active one is the one a line posts to.
        var account = reading.Accounts
            .Where(a => string.Equals(a.Code, trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => string.Equals(a.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        if (account is null)
            return XeroCodeResolution.Blocked($"Xero has no account {trimmed}; choose a {what} account Xero holds in Settings.");

        if (!string.Equals(account.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return XeroCodeResolution.Blocked($"Xero's account {account.Code} ({account.Name}) is {account.Status}, not active; choose another in Settings.");

        if (!IsOfPurpose(account, purpose))
            return XeroCodeResolution.Blocked($"Xero's account {account.Code} ({account.Name}) is not {(purpose == XeroAccountPurpose.Sales ? "a revenue" : "an expense")} account; choose another in Settings.");

        return XeroCodeResolution.Usable(account.Code!);
    }

    /// <summary>The accounts Settings may offer for <paramref name="purpose"/>: <c>ACTIVE</c>, with a code, of the purpose's class, in code order.</summary>
    /// <param name="reading">The last reading of Xero.</param>
    /// <param name="purpose">Sales or expense.</param>
    public static IReadOnlyList<XeroAccount> Choices(XeroSettingsReading reading, XeroAccountPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return [.. reading.Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.Code) && string.Equals(a.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && IsOfPurpose(a, purpose))
            .OrderBy(a => a.Code, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether <paramref name="account"/> is of <paramref name="purpose"/>'s class — Xero's <c>Class</c> when it states one, else its <c>Type</c>.</summary>
    private static bool IsOfPurpose(XeroAccount account, XeroAccountPurpose purpose)
    {
        if (!string.IsNullOrWhiteSpace(account.Class))
            return string.Equals(account.Class, purpose == XeroAccountPurpose.Sales ? "REVENUE" : "EXPENSE", StringComparison.OrdinalIgnoreCase);

        string[] types = purpose == XeroAccountPurpose.Sales
            ? ["REVENUE", "SALES", "OTHERINCOME"]
            : ["EXPENSE", "OVERHEADS", "DIRECTCOSTS", "DEPRECIATN"];
        return types.Contains(account.Type, StringComparer.OrdinalIgnoreCase);
    }

    private static void Register(ISettingsProvider settings, SettingDefinition definition)
    {
        try
        {
            settings.RegisterDefinition(definition);
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already.
        }
    }

    private async Task<string> ReadChoiceAsync(string key, string fallback, CancellationToken cancellationToken)
    {
        // Marked only once the definitions exist: a call racing the first one ensures them itself
        // (idempotent) rather than reading before they are registered and answering the default.
        if (Volatile.Read(ref _definitionsEnsured) == 0)
        {
            EnsureDefinitions(_settings);
            Volatile.Write(ref _definitionsEnsured, 1);
        }

        try
        {
            return await _settings.GetValueAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (SettingNotFoundException)
        {
            return fallback;
        }
    }
}
