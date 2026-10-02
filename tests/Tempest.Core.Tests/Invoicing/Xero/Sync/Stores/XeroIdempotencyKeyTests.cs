using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>`v0.24.0` B2: the <c>Idempotency-Key</c> is at most 128 characters, stable, and distinct where Xero must not replay.</summary>
public sealed class XeroIdempotencyKeyTests
{
    private const string Sha = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private static readonly XeroDocumentRef Quote = XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.Parse("3f2b8c1e-5d6a-4b7c-9e0f-112233445566"));

    [Fact]
    public void ShortEnough_KeyIsTheDocumentedReadableForm()
    {
        var key = XeroIdempotencyKey.Create(Quote, XeroOperation.PushQuote, Sha);

        Assert.Equal($"tos:Quote:3f2b8c1e-5d6a-4b7c-9e0f-112233445566:PushQuote:{Sha}", key);
        Assert.True(key.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength);
    }

    [Fact]
    public void SameInputs_SameKey_EveryTime()
    {
        var first = XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha, "SENT", 2);
        var second = XeroIdempotencyKey.Create(new XeroDocumentRef(XeroDocumentKind.Quote, Quote.TempestKey), XeroOperation.SetQuoteStatus, Sha, "SENT", 2);

        Assert.Equal(first, second);
    }

    [Fact]
    public void KnownValue_IsPinned_SoAnyChangeToTheAlgorithmIsSeen()
    {
        // Pinned: a key already sent to Xero must be reproduced verbatim by
        // every later build, or a resend after an upgrade would not be
        // recognised as the same request.
        var key = XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, "abc", "SENT");

        Assert.Equal(
            "tos:Quote:3f2b8c1e-5d6a-4b7c-9e0f-112233445566:SetQuoteStatus:f39e2c21868a80433aa4094261ce7975e4b40172e141ddb21eba55bc081a09f3",
            key);
    }

    public static TheoryData<XeroDocumentKind, XeroOperation> EveryKindAndOperation()
    {
        var data = new TheoryData<XeroDocumentKind, XeroOperation>();
        foreach (var kind in Enum.GetValues<XeroDocumentKind>())
        {
            foreach (var operation in Enum.GetValues<XeroOperation>())
                data.Add(kind, operation);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryKindAndOperation))]
    public void EveryKindAndOperation_WithALongHash_IsAtMost128AndWellFormed(XeroDocumentKind kind, XeroOperation operation)
    {
        var document = XeroDocumentRef.For(kind, Guid.Parse("ffffffff-ffff-4fff-bfff-ffffffffffff"));

        foreach (var hash in new[] { Sha, Sha + Sha, "x" })
        {
            foreach (var argument in new[] { null, "ACCEPTED", "a very long attachment file name.pdf" })
            {
                var key = XeroIdempotencyKey.Create(document, operation, hash, argument, occurrence: 3);

                Assert.True(key.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength, key);
                Assert.True(XeroIdempotencyKey.IsWellFormed(key), key);
            }
        }
    }

    [Fact]
    public void TooLong_IsShortenedByHashing_ToFourSegments()
    {
        var bill = XeroDocumentRef.For(XeroDocumentKind.ExpenseBill, Guid.Parse("3f2b8c1e-5d6a-4b7c-9e0f-112233445566"));

        var key = XeroIdempotencyKey.Create(bill, XeroOperation.PushExpenseBill, Sha);

        Assert.True(key.Length <= 128);
        Assert.Equal(["tos", "ExpenseBill", "PushExpenseBill"], key.Split(':')[..3]);
        Assert.Equal(4, key.Split(':').Length);
    }

    [Fact]
    public void ContactKeyWithUnsafeCharacters_IsHashed_AndStaysAscii()
    {
        var contact = new XeroDocumentRef(XeroDocumentKind.Contact, "Ørsted & Søn: Ltd");

        var key = XeroIdempotencyKey.Create(contact, XeroOperation.PushQuote, "h1");

        Assert.True(XeroIdempotencyKey.IsWellFormed(key));
        Assert.DoesNotContain("Ørsted", key, StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentOccurrenceOperationDocumentAndContent_EachChangeTheKey()
    {
        var keys = new[]
        {
            XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha, "SENT"),
            XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha, "ACCEPTED"),
            XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha, "SENT", 1),
            XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha),
            XeroIdempotencyKey.Create(Quote, XeroOperation.SetQuoteStatus, Sha, ""),
            XeroIdempotencyKey.Create(Quote, XeroOperation.PushQuote, Sha),
            XeroIdempotencyKey.Create(Quote, XeroOperation.PushQuote, Sha.Replace('9', '8')),
            XeroIdempotencyKey.Create(XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.Empty), XeroOperation.PushQuote, Sha),
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void BadInputs_AreRefused()
    {
        Assert.Throws<ArgumentNullException>(() => XeroIdempotencyKey.Create(null!, XeroOperation.PushQuote, Sha));
        Assert.Throws<ArgumentException>(() => XeroIdempotencyKey.Create(Quote, XeroOperation.PushQuote, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => XeroIdempotencyKey.Create(Quote, XeroOperation.PushQuote, Sha, occurrence: -1));
        Assert.False(XeroIdempotencyKey.IsWellFormed("other:key"));
        Assert.False(XeroIdempotencyKey.IsWellFormed("tos:" + new string('a', 125)));
    }
}
