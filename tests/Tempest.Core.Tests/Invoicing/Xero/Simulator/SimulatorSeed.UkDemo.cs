using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>One tax rate the simulated organisation holds.</summary>
/// <param name="TaxType">The code a line carries.</param>
/// <param name="Name">Xero's display name.</param>
/// <param name="EffectiveRate">The rate, as a percentage.</param>
/// <param name="Status">Xero's status word; only <c>ACTIVE</c> may be posted.</param>
/// <param name="CanApplyToRevenue">Whether a sales line (quote, ACCREC invoice) may use it.</param>
/// <param name="CanApplyToExpenses">Whether a purchase line (ACCPAY bill, purchase order) may use it.</param>
/// <param name="ReportTaxType">Xero's <c>ReportTaxType</c>.</param>
internal sealed record SimulatedTaxRate(string TaxType, string Name, decimal EffectiveRate, string Status, bool CanApplyToRevenue, bool CanApplyToExpenses, string ReportTaxType);

/// <summary>One account in the simulated chart of accounts.</summary>
/// <param name="AccountId">Xero's <c>AccountID</c>.</param>
/// <param name="Code">The code a line carries.</param>
/// <param name="Name">The account's name.</param>
/// <param name="Type">Xero's account type (<c>REVENUE</c>, <c>DIRECTCOSTS</c>, <c>OVERHEADS</c>, <c>BANK</c>, …).</param>
/// <param name="Class">Xero's account class (<c>REVENUE</c>, <c>EXPENSE</c>, <c>ASSET</c>, …).</param>
/// <param name="Status">Xero's status word (<c>ACTIVE</c>, <c>ARCHIVED</c>).</param>
/// <param name="TaxType">The account's default tax type.</param>
/// <param name="BankAccountNumber">For a bank account, its number.</param>
/// <param name="UpdatedUtc">When it last changed (<c>If-Modified-Since</c> on <c>GET Accounts</c>).</param>
internal sealed record SimulatedAccount(
    string AccountId, string Code, string Name, string Type, string Class, string Status, string? TaxType, string? BankAccountNumber, DateTimeOffset UpdatedUtc);

/// <summary>
/// The UK Demo Company as the simulator seeds it (design §10.1): the nine
/// UK VAT tax types TempestOS maps to, a subset of the chart of accounts
/// (<c>200</c> Sales, <c>310</c> Cost of Goods Sold, the <c>400</c>-series
/// overheads incl. <c>493</c> Travel, <c>090</c> Business Bank Account), and
/// the organisation's own details. Names and codes follow Xero's UK Demo
/// Company; addresses and numbers are illustrative. Two records are
/// simulator-only, to exercise "exists but not ACTIVE": tax type
/// <c>OUTPUT</c> (the pre-2011 17.5 % rate, <c>DELETED</c>) and account
/// <c>499</c> (<c>ARCHIVED</c>).
/// </summary>
internal static class SimulatorSeed
{
    /// <summary>The fixed <c>OrganisationID</c>.</summary>
    public const string OrganisationId = "5c3e8a3d-0000-4000-8000-00000000d3e0";

    /// <summary>When the seeded accounts were last changed — before any test clock, so <c>If-Modified-Since</c> excludes them.</summary>
    public static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The UK Demo Company's tax rates.</summary>
    public static List<SimulatedTaxRate> UkDemoTaxRates() =>
    [
        new("OUTPUT2", "20% (VAT on Income)", 20.0000m, "ACTIVE", true, false, "OUTPUT"),
        new("RROUTPUT", "5% (VAT on Income)", 5.0000m, "ACTIVE", true, false, "OUTPUT"),
        new("ZERORATEDOUTPUT", "Zero Rated Income", 0.0000m, "ACTIVE", true, false, "OUTPUT"),
        new("EXEMPTOUTPUT", "Exempt Income", 0.0000m, "ACTIVE", true, false, "EXEMPTOUTPUT"),
        new("INPUT2", "20% (VAT on Expenses)", 20.0000m, "ACTIVE", false, true, "INPUT"),
        new("RRINPUT", "5% (VAT on Expenses)", 5.0000m, "ACTIVE", false, true, "INPUT"),
        new("ZERORATEDINPUT", "Zero Rated Expenses", 0.0000m, "ACTIVE", false, true, "INPUT"),
        new("EXEMPTINPUT", "Exempt Expenses", 0.0000m, "ACTIVE", false, true, "EXEMPTINPUT"),
        new("NONE", "No VAT", 0.0000m, "ACTIVE", true, true, "NONE"),
        new("OUTPUT", "17.5% (VAT on Income)", 17.5000m, "DELETED", true, false, "OUTPUT"),
    ];

    /// <summary>The seeded subset of the UK Demo Company's chart of accounts.</summary>
    public static List<SimulatedAccount> UkDemoAccounts() =>
    [
        Account("090", "Business Bank Account", "BANK", "ASSET", "NONE", bankAccountNumber: "12-34-56 12345678"),
        Account("200", "Sales", "REVENUE", "REVENUE", "OUTPUT2"),
        Account("260", "Other Revenue", "REVENUE", "REVENUE", "OUTPUT2"),
        Account("310", "Cost of Goods Sold", "DIRECTCOSTS", "EXPENSE", "INPUT2"),
        Account("400", "Advertising", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("404", "Bank Fees", "OVERHEADS", "EXPENSE", "EXEMPTINPUT"),
        Account("408", "Cleaning", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("412", "Consulting & Accounting", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("420", "Entertainment", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("429", "General Expenses", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("433", "Insurance", "OVERHEADS", "EXPENSE", "EXEMPTINPUT"),
        Account("449", "Motor Vehicle Expenses", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("461", "Printing & Stationery", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("473", "Repairs and Maintenance", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("489", "Telephone & Internet", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("493", "Travel - National", "OVERHEADS", "EXPENSE", "INPUT2"),
        Account("494", "Travel - International", "OVERHEADS", "EXPENSE", "ZERORATEDINPUT"),
        Account("499", "Old Overheads (simulator-only, archived)", "OVERHEADS", "EXPENSE", "INPUT2", status: "ARCHIVED"),
    ];

    /// <summary>The <c>GET Organisation</c> element for <paramref name="options"/>.</summary>
    public static JsonObject UkDemoOrganisation(XeroSimulatorOptions options, DateTimeOffset createdUtc) => new()
    {
        ["OrganisationID"] = OrganisationId,
        ["Name"] = options.OrganisationName,
        ["LegalName"] = options.OrganisationName,
        ["PaysTax"] = true,
        ["Version"] = "UK",
        ["OrganisationType"] = "COMPANY",
        ["OrganisationEntityType"] = "COMPANY",
        ["BaseCurrency"] = "GBP",
        ["CountryCode"] = "GB",
        ["IsDemoCompany"] = options.IsDemoCompany,
        ["OrganisationStatus"] = "ACTIVE",
        ["RegistrationNumber"] = "01234567",
        ["TaxNumber"] = "GB 123 4567 89",
        ["FinancialYearEndDay"] = 31,
        ["FinancialYearEndMonth"] = 3,
        ["SalesTaxBasis"] = "ACCRUALS",
        ["SalesTaxPeriod"] = "QUARTERLY",
        ["DefaultSalesTax"] = "Tax Exclusive",
        ["DefaultPurchasesTax"] = "Tax Exclusive",
        ["Timezone"] = "GMTSTANDARDTIME",
        ["ShortCode"] = "!d3e0x",
        ["LineOfBusiness"] = "Engineering consultancy",
        ["CreatedDateUTC"] = XeroWire.MsDate(createdUtc),
        ["Addresses"] = new JsonArray
        {
            new JsonObject
            {
                ["AddressType"] = "STREET",
                ["AddressLine1"] = "23 Main Street",
                ["AddressLine2"] = "Central City",
                ["City"] = "Marineville",
                ["Region"] = "Wessex",
                ["PostalCode"] = "MA12 3BC",
                ["Country"] = "United Kingdom",
            },
            new JsonObject
            {
                ["AddressType"] = "POBOX",
                ["AddressLine1"] = "PO Box 1234",
                ["City"] = "Marineville",
                ["PostalCode"] = "MA12 9ZZ",
                ["Country"] = "United Kingdom",
            },
        },
        ["Phones"] = new JsonArray
        {
            new JsonObject { ["PhoneType"] = "OFFICE", ["PhoneNumber"] = "01234 567890" },
        },
        ["ExternalLinks"] = new JsonArray
        {
            new JsonObject { ["LinkType"] = "Website", ["Url"] = "https://www.example.co.uk" },
        },
        ["PaymentTerms"] = new JsonObject
        {
            ["Sales"] = new JsonObject { ["Day"] = 30, ["Type"] = "DAYSAFTERBILLDATE" },
            ["Bills"] = new JsonObject { ["Day"] = 30, ["Type"] = "DAYSAFTERBILLDATE" },
        },
    };

    /// <summary>The <c>GET TaxRates</c> element for <paramref name="rate"/>.</summary>
    public static JsonObject ToJson(SimulatedTaxRate rate) => new()
    {
        ["Name"] = rate.Name,
        ["TaxType"] = rate.TaxType,
        ["ReportTaxType"] = rate.ReportTaxType,
        ["CanApplyToAssets"] = rate.CanApplyToExpenses,
        ["CanApplyToEquity"] = false,
        ["CanApplyToExpenses"] = rate.CanApplyToExpenses,
        ["CanApplyToLiabilities"] = rate.CanApplyToExpenses,
        ["CanApplyToRevenue"] = rate.CanApplyToRevenue,
        ["DisplayTaxRate"] = rate.EffectiveRate,
        ["EffectiveRate"] = rate.EffectiveRate,
        ["Status"] = rate.Status,
        ["TaxComponents"] = new JsonArray
        {
            new JsonObject { ["Name"] = "VAT", ["Rate"] = rate.EffectiveRate, ["IsCompound"] = false, ["IsNonRecoverable"] = false },
        },
    };

    /// <summary>The <c>GET Accounts</c> element for <paramref name="account"/>.</summary>
    public static JsonObject ToJson(SimulatedAccount account)
    {
        var json = new JsonObject
        {
            ["AccountID"] = account.AccountId,
            ["Code"] = account.Code,
            ["Name"] = account.Name,
            ["Type"] = account.Type,
            ["Class"] = account.Class,
            ["Status"] = account.Status,
            ["TaxType"] = account.TaxType,
            ["EnablePaymentsToAccount"] = false,
            ["ShowInExpenseClaims"] = account.Class == "EXPENSE",
            ["ReportingCode"] = account.Class,
            ["UpdatedDateUTC"] = XeroWire.MsDate(account.UpdatedUtc),
        };
        if (account.BankAccountNumber is not null)
        {
            json["BankAccountNumber"] = account.BankAccountNumber;
            json["BankAccountType"] = "BANK";
            json["CurrencyCode"] = "GBP";
        }

        return json;
    }

    private static SimulatedAccount Account(string code, string name, string type, string cls, string taxType, string status = "ACTIVE", string? bankAccountNumber = null) =>
        new(DeterministicId("account", code), code, name, type, cls, status, taxType, bankAccountNumber, SeededAtUtc);

    /// <summary>A stable GUID-shaped id for a seeded record, so tests and fixtures can name it.</summary>
    public static string DeterministicId(string kind, string key)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{kind}:{key}"));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
