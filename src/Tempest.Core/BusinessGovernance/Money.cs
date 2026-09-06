using System.Globalization;

namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// An exact monetary amount in a stated currency.
/// </summary>
/// <remarks>
/// <para>
/// <b>Money is not a <see cref="Tempest.Core.UnitsAndQuantities.Quantity{TDimension}"/>.</b>
/// Currency is not a physical dimension: there is no conversion factor
/// between pounds and euros that is true independently of a date and a
/// market, and treating one as a unit would invite exactly the silent,
/// wrong conversion that the units layer exists to prevent. `P07`
/// therefore carries its own financial type and does not borrow the
/// engineering one.
/// </para>
/// <para>
/// <b>The amount is <see cref="decimal"/>, never <see cref="double"/>.</b>
/// A rate card, an invoice line and a forecast are exact to the minor
/// unit, and binary floating point cannot represent 0.1 exactly. Every
/// arithmetic operation here is decimal arithmetic.
/// </para>
/// <para>
/// <b>Arithmetic across currencies is refused, not converted.</b> Adding
/// £100 to €100 has no answer without a rate and a date, and returning
/// one would be inventing a financial fact. The same discipline
/// `ADR-0125` applied to affine units.
/// </para>
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>Initialises a new instance of the <see cref="Money"/> struct.</summary>
    /// <param name="amount">The amount, exact to the currency's own minor unit.</param>
    /// <param name="currency">The currency the amount is stated in.</param>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is not a well-formed currency code.</exception>
    public Money(decimal amount, CurrencyCode currency)
    {
        if (!currency.IsSpecified)
            throw new ArgumentException("A monetary amount must state its currency. An unqualified number is not money.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount, exact to the currency's own minor unit.</summary>
    public decimal Amount { get; }

    /// <summary>The currency the amount is stated in.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Whether the amount is zero.</summary>
    public bool IsZero => Amount == 0m;

    /// <summary>Whether the amount is below zero — a credit, a loss or, often, a data-entry error.</summary>
    public bool IsNegative => Amount < 0m;

    /// <summary>A zero amount in <paramref name="currency"/>.</summary>
    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    /// <summary>Adds two amounts in the same currency.</summary>
    /// <exception cref="CurrencyMismatchException">The two amounts are in different currencies.</exception>
    public static Money operator +(Money left, Money right)
    {
        Require(left.Currency, right.Currency);

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts one amount from another in the same currency.</summary>
    /// <exception cref="CurrencyMismatchException">The two amounts are in different currencies.</exception>
    public static Money operator -(Money left, Money right)
    {
        Require(left.Currency, right.Currency);

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Negates an amount.</summary>
    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    /// <summary>Scales an amount by a dimensionless factor — a quantity of hours, a percentage, a headcount.</summary>
    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor, left.Currency);

    /// <summary>Scales an amount by a dimensionless factor.</summary>
    public static Money operator *(decimal factor, Money right) => right * factor;

    /// <summary>Divides an amount by a dimensionless divisor.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="divisor"/> is zero.</exception>
    public static Money operator /(Money left, decimal divisor) => new(left.Amount / divisor, left.Currency);

    /// <summary>Compares two amounts in the same currency.</summary>
    /// <exception cref="CurrencyMismatchException">The two amounts are in different currencies.</exception>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="op_LessThan"/>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="op_LessThan"/>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="op_LessThan"/>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>Sums <paramref name="amounts"/>, which must all be in <paramref name="currency"/>.</summary>
    /// <remarks>
    /// The currency is supplied rather than taken from the first element,
    /// so summing an empty sequence still yields an answer in a stated
    /// currency instead of throwing or guessing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="amounts"/> is <see langword="null"/>.</exception>
    /// <exception cref="CurrencyMismatchException">An element is in a different currency.</exception>
    public static Money Sum(IEnumerable<Money> amounts, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        var total = 0m;

        foreach (var amount in amounts)
        {
            Require(currency, amount.Currency);
            total += amount.Amount;
        }

        return new Money(total, currency);
    }

    /// <summary>Rounds to <paramref name="decimalPlaces"/> using banker's rounding, the convention accounting systems use.</summary>
    public Money RoundTo(int decimalPlaces) => new(Math.Round(Amount, decimalPlaces, MidpointRounding.ToEven), Currency);

    /// <inheritdoc />
    public int CompareTo(Money other)
    {
        Require(Currency, other.Currency);

        return Amount.CompareTo(other.Amount);
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Amount.ToString("0.00##", CultureInfo.InvariantCulture)} {Currency}";

    private static void Require(CurrencyCode left, CurrencyCode right)
    {
        if (left != right)
            throw new CurrencyMismatchException(left, right);
    }
}

/// <summary>
/// An ISO 4217 three-letter currency code.
/// </summary>
/// <remarks>
/// A code, not an enumeration of the currencies TempestOS happens to know
/// about: a closed enum would silently exclude a client's own currency,
/// and there is no engineering reason for this platform to curate a list
/// of the world's currencies. The code is validated for shape only — three
/// ASCII letters — and is not checked against a registry, because no
/// registry ships with this platform and pretending otherwise would be
/// asserting something unverified.
/// </remarks>
public readonly record struct CurrencyCode : IComparable<CurrencyCode>
{
    private readonly string? _code;

    /// <summary>Initialises a new instance of the <see cref="CurrencyCode"/> struct.</summary>
    /// <param name="code">A three-letter ISO 4217 code, case-insensitive.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is not three ASCII letters.</exception>
    public CurrencyCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var trimmed = code.Trim();

        if (trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
            throw new ArgumentException(
                $"'{code}' is not a three-letter currency code. TempestOS validates the shape of a currency code, not its existence: "
                + "no currency registry ships with this platform.",
                nameof(code));

        _code = trimmed.ToUpperInvariant();
    }

    /// <summary>Whether a code was actually supplied — <see langword="false"/> for the default value.</summary>
    public bool IsSpecified => _code is not null;

    /// <summary>Pound sterling, the currency TempestOS's own fixtures are stated in.</summary>
    public static CurrencyCode Gbp { get; } = new("GBP");

    /// <inheritdoc />
    public int CompareTo(CurrencyCode other) => string.CompareOrdinal(_code, other._code);

    /// <inheritdoc />
    public override string ToString() => _code ?? "(unspecified)";
}

/// <summary>Thrown when an operation would combine or compare amounts in different currencies.</summary>
/// <remarks>
/// An exception rather than a conversion, deliberately: converting needs a
/// rate and a date, both of which are financial facts this platform does
/// not hold. Refusing is the only honest answer.
/// </remarks>
public sealed class CurrencyMismatchException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="CurrencyMismatchException"/> class.</summary>
    /// <param name="expected">The currency the operation was working in.</param>
    /// <param name="actual">The currency it was handed.</param>
    public CurrencyMismatchException(CurrencyCode expected, CurrencyCode actual)
        : base($"Cannot combine an amount in {actual} with an amount in {expected}. Converting between currencies needs an exchange "
               + "rate and a date, neither of which TempestOS holds, so the operation is refused rather than answered.")
    {
        Expected = expected;
        Actual = actual;
    }

    /// <summary>The currency the operation was working in.</summary>
    public CurrencyCode Expected { get; }

    /// <summary>The currency it was handed.</summary>
    public CurrencyCode Actual { get; }
}
