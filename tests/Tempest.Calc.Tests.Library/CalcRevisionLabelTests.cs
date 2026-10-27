using Tempest.Calc.Library;

namespace Tempest.Calc.Tests.Library;

/// <summary>DC-15: revision labels after Z run AA, AB … AZ, BA … ZZ, AAA like spreadsheet columns; the old Z+, Z++ labels still read and sort.</summary>
public sealed class CalcRevisionLabelTests
{
    [Theory]
    [InlineData("A", "B")]
    [InlineData("Y", "Z")]
    [InlineData("Z", "AA")]
    [InlineData("AA", "AB")]
    [InlineData("AY", "AZ")]
    [InlineData("AZ", "BA")]
    [InlineData("BZ", "CA")]
    [InlineData("ZY", "ZZ")]
    [InlineData("ZZ", "AAA")]
    [InlineData("AAA", "AAB")]
    [InlineData("ZZZ", "AAAA")]
    [InlineData("1", "2")]
    [InlineData("9", "10")]
    [InlineData("99", "100")]
    public void Next_Follows_The_Spreadsheet_Column_Order(string from, string to) => Assert.Equal(to, CalcRevisionLabel.Next(from));

    [Theory]
    [InlineData("Z+", "AB")]
    [InlineData("Z++", "AC")]
    [InlineData("Z+++", "AD")]
    public void A_Label_Stored_Before_DC_15_Reads_As_The_27th_And_Later_Revision_And_Carries_On_In_Letters(string stored, string next) =>
        Assert.Equal(next, CalcRevisionLabel.Next(stored));

    [Theory]
    [InlineData("A", 1)]
    [InlineData("Z", 26)]
    [InlineData("AA", 27)]
    [InlineData("AZ", 52)]
    [InlineData("BA", 53)]
    [InlineData("ZZ", 702)]
    [InlineData("AAA", 703)]
    [InlineData("Z+", 27)]
    [InlineData("Z++", 28)]
    [InlineData("7", 7)]
    public void A_Label_Has_A_Place_In_The_Order_Revisions_Were_Made(string label, long position)
    {
        Assert.True(CalcRevisionLabel.TryGetPosition(label, out var found));
        Assert.Equal(position, found);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("A1")]
    [InlineData("Y+")]
    [InlineData("AZ+")]
    [InlineData("+")]
    [InlineData("ABCDEFGHIJKLM")]
    public void A_Label_That_Is_None_Of_These_Is_Not_Understood_And_Gets_A_Plus(string label)
    {
        Assert.False(CalcRevisionLabel.TryGetPosition(label, out _));
        Assert.Equal(label + "+", CalcRevisionLabel.Next(label));
    }

    [Fact]
    public void The_First_702_Labels_Are_Distinct_Two_Letters_At_Most_And_Each_Next_Is_The_Following_Position()
    {
        var label = "A";
        var seen = new HashSet<string> { label };

        for (var position = 2; position <= 702; position++)
        {
            var next = CalcRevisionLabel.Next(label);
            Assert.Equal(CalcRevisionLabel.FromPosition(position), next);
            Assert.True(next.Length <= 2);
            Assert.True(seen.Add(next), $"{next} came twice.");
            Assert.True(CalcRevisionLabel.TryGetPosition(next, out var found) && found == position);
            label = next;
        }

        Assert.Equal("ZZ", label);
    }

    [Fact]
    public void The_101st_Revision_Is_A_Two_Letter_Label_Not_A_76_Character_One()
    {
        var label = "A";

        for (var i = 1; i < 101; i++)
            label = CalcRevisionLabel.Next(label);

        Assert.Equal("CW", label);
    }

    [Fact]
    public void Labels_Sort_As_They_Were_Made_Not_As_Text_And_Old_Labels_Sort_Among_The_New()
    {
        string[] made = ["A", "B", "Y", "Z", "Z++", "AC", "AZ", "BA", "ZZ", "AAA"];
        string[] shuffled = [made[8], made[3], made[0], made[9], made[4], made[6], made[1], made[7], made[5], made[2]];
        Assert.Equal(made, shuffled.Order(CalcRevisionLabel.Comparer));

        // As text AA sorts before B and AAA before AB; by place it does not.
        Assert.True(string.CompareOrdinal("AA", "B") < 0);
        Assert.True(CalcRevisionLabel.Compare("AA", "B") > 0);
        Assert.True(CalcRevisionLabel.Compare("ZZ", "AAA") < 0);
        Assert.True(CalcRevisionLabel.Compare("9", "10") < 0);
        Assert.Equal(0, CalcRevisionLabel.Compare("Z+", "AA"));
    }

    [Fact]
    public void A_Label_That_Is_Not_Understood_Sorts_After_Every_One_That_Is()
    {
        string[] mixed = ["zzz", "ZZ", "A1", "B"];
        Assert.Equal(["B", "ZZ", "A1", "zzz"], mixed.Order(CalcRevisionLabel.Comparer));
    }
}
