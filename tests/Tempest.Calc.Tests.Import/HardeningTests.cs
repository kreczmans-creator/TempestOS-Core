using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Import;

namespace Tempest.Calc.Tests.Import;

/// <summary>Addendum B §B6 W1 security: the bounded reader, strict numbers, path canonicalisation, CSV in and out.</summary>
public sealed class HardeningTests
{
    // ---- bounded reader ------------------------------------------------

    [Fact]
    public async Task A_File_Over_Five_Megabytes_Is_Refused_And_One_At_The_Cap_Is_Read()
    {
        var atCap = new byte[ImportLimits.DefaultMaxBytes];
        var read = await BoundedInputReader.ReadAsync(new MemoryStream(atCap), "cap.csv");
        Assert.Equal(ImportLimits.DefaultMaxBytes, read.Length);

        var over = new byte[ImportLimits.DefaultMaxBytes + 1];
        var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadAsync(new MemoryStream(over), "over.csv"));
        Assert.Equal(ImportRefusalCode.TooLarge, ex.Code);
        Assert.Contains("5", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Stream_That_Lies_About_Its_Length_Or_Never_Ends_Stops_At_The_Cap_Without_Reading_On()
    {
        var endless = new EndlessStream();
        var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadAsync(endless, "endless.csv"));
        Assert.Equal(ImportRefusalCode.TooLarge, ex.Code);

        // 64 KB chunks: the reader stopped within one chunk of the cap, it did not drain the stream.
        Assert.InRange(endless.Produced, ImportLimits.DefaultMaxBytes, ImportLimits.DefaultMaxBytes + (64 * 1024));
    }

    [Fact]
    public async Task A_Stalled_Stream_Is_Stopped_By_The_Time_Limit_And_The_Callers_Cancellation_Is_Not_A_Refusal()
    {
        var limits = new ImportLimits { Timeout = TimeSpan.FromMilliseconds(150) };
        var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadAsync(new StalledStream(), "stalled.csv", limits));
        Assert.Equal(ImportRefusalCode.Timeout, ex.Code);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BoundedInputReader.ReadAsync(new StalledStream(), "stalled.csv", null, cts.Token));
    }

    [Fact]
    public void The_Budget_Throws_A_Timeout_Refusal_Once_The_Time_Has_Passed()
    {
        var budget = new ImportBudget(new ImportLimits { Timeout = TimeSpan.FromMilliseconds(1) });
        Thread.Sleep(30);
        Samples.Refuses(ImportRefusalCode.Timeout, budget.Check);
    }

    [Fact]
    public void The_Hash_Is_The_Sha256_Of_The_Bytes()
    {
        var bytes = "abc"u8.ToArray();
        var file = BoundedInputReader.FromBytes(bytes, "a.txt");
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), file.Sha256);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", file.Sha256);
    }

    [Fact]
    public void Utf8_Is_Read_With_Or_Without_A_Byte_Order_Mark_And_Other_Encodings_Are_Refused()
    {
        var plain = Samples.Bytes("name=é"u8.ToArray());
        var bom = Samples.Bytes([0xEF, 0xBB, 0xBF, .. "name=é"u8.ToArray()]);
        Assert.Equal("name=é", BoundedInputReader.DecodeText(plain));
        Assert.Equal("name=é", BoundedInputReader.DecodeText(bom));

        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => BoundedInputReader.DecodeText(Samples.Bytes([0xFF, 0xFE, 0x61, 0x00])));
        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => BoundedInputReader.DecodeText(Samples.Bytes([0xFE, 0xFF, 0x00, 0x61])));
        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => BoundedInputReader.DecodeText(Samples.Bytes([0x61, 0xC3, 0x28, 0x62])));
        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => BoundedInputReader.DecodeText(Samples.Bytes([0xED, 0xA0, 0x80])));
    }

    [Theory]
    [InlineData("a\0b")]
    [InlineData("a\u001Bb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0085b")]
    [InlineData("a\u0008b")]
    [InlineData("a\u000Bb")]
    public void A_Control_Character_Other_Than_Tab_Cr_Lf_Is_Refused_With_Its_Line(string text)
    {
        var ex = Samples.Refuses(ImportRefusalCode.ControlCharacter, () => BoundedInputReader.DecodeText(Samples.Text("ok\nfine\n" + text)));
        Assert.Contains("line 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tab_Carriage_Return_And_Line_Feed_Are_Allowed()
    {
        Assert.Equal("a\tb\r\nc\n", BoundedInputReader.DecodeText(Samples.Text("a\tb\r\nc\n")));
    }

    // ---- numbers -------------------------------------------------------

    [Theory]
    [InlineData("40.5", 40.5)]
    [InlineData("-3", -3)]
    [InlineData("+3.25", 3.25)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5)]
    [InlineData("1e3", 1000)]
    [InlineData("1.5E-3", 0.0015)]
    [InlineData("  12  ", 12)]
    public void Decimal_Point_Numbers_Are_Read(string text, double expected) =>
        Assert.Equal(expected, LocaleNumber.Parse(text, NumberProfile.Point), 12);

    [Theory]
    [InlineData("40,5", 40.5)]
    [InlineData("-0,25", -0.25)]
    [InlineData("1,5e1", 15)]
    [InlineData("1500", 1500)]
    public void Decimal_Comma_Numbers_Are_Read_Under_A_Decimal_Comma_Profile(string text, double expected) =>
        Assert.Equal(expected, LocaleNumber.Parse(text, NumberProfile.Comma), 12);

    [Fact]
    public void A_Number_In_The_Other_Notation_Is_Refused_Never_Reinterpreted()
    {
        Assert.False(LocaleNumber.TryParse("1,5", NumberProfile.Point, out _, out var problem));
        Assert.Contains("decimal mark", problem, StringComparison.Ordinal);
        Assert.False(LocaleNumber.TryParse("1.5", NumberProfile.Comma, out _, out _));
        Assert.False(LocaleNumber.TryParse("1,234.5", NumberProfile.Point, out _, out _));
        Assert.False(LocaleNumber.TryParse("1.234,5", NumberProfile.Comma, out _, out _));
        Assert.True(LocaleNumber.LooksLikeOtherMark("1,5", NumberProfile.Point));
        Assert.True(LocaleNumber.LooksLikeOtherMark("1.5", NumberProfile.Comma));
        Assert.False(LocaleNumber.LooksLikeOtherMark("1.5", NumberProfile.Point));
    }

    [Fact]
    public void Grouping_Is_Only_Read_When_The_Profile_Declares_It_And_Only_In_Groups_Of_Three()
    {
        var point = new NumberProfile(DecimalMark.Point, ',');
        var comma = new NumberProfile(DecimalMark.Comma, '.');
        Assert.Equal(1234.5, LocaleNumber.Parse("1,234.5", point), 12);
        Assert.Equal(1_234_567, LocaleNumber.Parse("1,234,567", point), 12);
        Assert.Equal(1234.5, LocaleNumber.Parse("1.234,5", comma), 12);
        Assert.False(LocaleNumber.TryParse("1,23,456", point, out _, out _));
        Assert.False(LocaleNumber.TryParse("1234,567", point, out _, out _));
        Assert.False(LocaleNumber.TryParse(",234", point, out _, out _));
        Assert.False(new NumberProfile(DecimalMark.Point, '.').IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("0x10")]
    [InlineData("1_000")]
    [InlineData("1 000")]
    [InlineData("--1")]
    [InlineData("1.2.3")]
    [InlineData("١٢٣")]
    [InlineData("1−2")]
    [InlineData("−5")]
    [InlineData("5mm")]
    [InlineData("1e99999")]
    public void Garbage_Is_Not_A_Number(string text) =>
        Assert.False(LocaleNumber.TryParse(text, NumberProfile.Point, out _, out var problem) || problem.Length == 0);

    [Fact]
    public void A_Number_Longer_Than_The_Cap_Or_Out_Of_Range_Is_Refused_With_The_Number_Format_Code()
    {
        Samples.Refuses(ImportRefusalCode.NumberFormat, () => LocaleNumber.Parse(new string('9', 41), NumberProfile.Point));
        Samples.Refuses(ImportRefusalCode.NumberFormat, () => LocaleNumber.Parse("1e999", NumberProfile.Point));
    }

    // ---- paths ---------------------------------------------------------

    [Theory]
    [InlineData("../secret.exp")]
    [InlineData("a/../../secret.exp")]
    [InlineData("a\\..\\secret.exp")]
    [InlineData("/etc/../etc/passwd")]
    [InlineData("\\\\server\\share\\file.exp")]
    [InlineData("//server/share/file.exp")]
    [InlineData("\\\\.\\COM1")]
    [InlineData("\\\\?\\C:\\file.exp")]
    [InlineData("/dev/zero")]
    [InlineData("/proc/self/environ")]
    [InlineData("C:file.exp")]
    [InlineData("C:\\dir\\file.exp:stream")]
    [InlineData("data/file.exp:Zone.Identifier")]
    [InlineData("CON")]
    [InlineData("nul.exp")]
    [InlineData("dir/COM3.txt")]
    [InlineData("dir/trailing. ")]
    [InlineData("dir/trailing.")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b.exp")]
    [InlineData("a\u202Eb.exp")]
    public void A_Path_That_Traverses_Reaches_A_Share_Or_Device_Or_Hides_Something_Is_Refused(string path) =>
        Assert.Equal(ImportRefusalCode.UnsafePath, Assert.Throws<ImportRefusedException>(() => ImportPath.Canonicalise(path)).Code);

    [Fact]
    public void A_Path_Is_Held_Inside_Its_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "w1-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var inside = ImportPath.Canonicalise("sub/part.exp", root);
            Assert.StartsWith(Path.GetFullPath(root), inside, StringComparison.Ordinal);
            Samples.Refuses(ImportRefusalCode.UnsafePath, () => ImportPath.Canonicalise(Path.Combine(Path.GetTempPath(), "elsewhere.exp"), root));
            Samples.Refuses(ImportRefusalCode.UnsafePath, () => ImportPath.Canonicalise(root + "-sibling/part.exp", root));
            Samples.Refuses(ImportRefusalCode.UnsafePath, () => ImportPath.Canonicalise("../x.exp", root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_File_That_Is_A_Link_Is_Not_Followed_And_A_Missing_File_Is_Reported()
    {
        var root = Path.Combine(Path.GetTempPath(), "w1-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var real = Path.Combine(root, "real.exp");
            await File.WriteAllTextAsync(real, "[mm]a=1\n");
            var good = await BoundedInputReader.ReadFileAsync(real, root);
            Assert.Equal("real.exp", good.FileName);

            var link = Path.Combine(root, "link.exp");

            try
            {
                File.CreateSymbolicLink(link, real);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // symbolic links need a privilege this machine does not give; the Linux CI leg runs this.
            }

            var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadFileAsync(link, root));
            Assert.Equal(ImportRefusalCode.UnsafePath, ex.Code);

            var missing = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadFileAsync(Path.Combine(root, "nope.exp"), root));
            Assert.Equal(ImportRefusalCode.NotFound, missing.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("C:\\Users\\me\\bracket.exp", "bracket.exp")]
    [InlineData("/home/me/bracket.exp", "bracket.exp")]
    [InlineData("..\\..\\bracket.exp", "bracket.exp")]
    [InlineData("a\u202Eb.exp", "ab.exp")]
    [InlineData("", "file")]
    [InlineData("/", "file")]
    public void A_File_Name_Is_Shown_As_A_Safe_Leaf(string name, string expected) => Assert.Equal(expected, ImportPath.SafeLeafName(name));

    [Fact]
    public void An_Extension_Outside_The_List_Is_Refused()
    {
        ImportPath.RequireExtension("a.EXP", ".exp", ".xlsx");
        Samples.Refuses(ImportRefusalCode.Unsupported, () => ImportPath.RequireExtension("a.exe", ".exp", ".xlsx"));
    }

    // ---- csv in --------------------------------------------------------

    [Fact]
    public void Rfc4180_Quotes_Embedded_Line_Breaks_And_Doubled_Quotes_Are_Read()
    {
        var table = CsvReader.Read("a,b,c\r\n1,\"x,y\",\"say \"\"hi\"\"\"\r\n\"line1\nline2\",,3\n");
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["a", "b", "c"], table.Rows[0]);
        Assert.Equal(["1", "x,y", "say \"hi\""], table.Rows[1]);
        Assert.Equal(["line1\nline2", "", "3"], table.Rows[2]);
        Assert.Equal([1, 2, 3], table.Lines);
    }

    [Fact]
    public void Blank_Lines_Are_Skipped_And_A_Missing_Final_Newline_Is_Fine()
    {
        var table = CsvReader.Read("a;b\n\n\n1;2", ';');
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["1", "2"], table.Rows[1]);
    }

    [Theory]
    [InlineData("a,\"b")]
    [InlineData("a,b\"c,d")]
    [InlineData("a,\"b\"c")]
    [InlineData("\"")]
    public void Malformed_Quoting_Is_Refused(string text) =>
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CsvReader.Read(text));

    [Fact]
    public void The_Row_Column_And_Field_Caps_Hold_While_Reading()
    {
        var limits = new ImportLimits { MaxRows = 5, MaxColumns = 3, MaxFieldChars = 10 };
        var budget = new ImportBudget(limits);
        Samples.Refuses(ImportRefusalCode.TooManyRows, () => CsvReader.Read(string.Join('\n', Enumerable.Range(0, 6).Select(i => "a,b")), ',', budget));
        Samples.Refuses(ImportRefusalCode.TooManyColumns, () => CsvReader.Read("a,b,c,d", ',', budget));
        Samples.Refuses(ImportRefusalCode.FieldTooLong, () => CsvReader.Read("12345678901,b", ',', budget));
        Samples.Refuses(ImportRefusalCode.FieldTooLong, () => CsvReader.Read("\"12345678901\",b", ',', budget));
        Assert.Equal(5, CsvReader.Read(string.Join('\n', Enumerable.Range(0, 5).Select(i => "a,b")), ',', budget).Rows.Count);
    }

    [Theory]
    [InlineData("a,b,c\n", ',')]
    [InlineData("a;b;c\n", ';')]
    [InlineData("a\tb\tc\n", '\t')]
    [InlineData("\"a,b\";c;d\n", ';')]
    [InlineData("abc\n", ',')]
    public void The_Delimiter_Is_Suggested_From_The_First_Record_Outside_Quotes(string text, char expected) =>
        Assert.Equal(expected, CsvReader.DetectDelimiter(text));

    // ---- csv out -------------------------------------------------------

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(A1)")]
    [InlineData("\t=1")]
    [InlineData("\r=1")]
    [InlineData("\n=1+1")]
    [InlineData("  \n@SUM(1)")]
    [InlineData("  =1+1")]
    [InlineData("\u00A0=1+1")]
    [InlineData("\uFF1D1+1")]
    [InlineData("=cmd|' /C calc'!A0")]
    [InlineData("-2+3+cmd|' /C calc'!A0")]
    public void A_Text_That_A_Spreadsheet_Would_Run_Is_Defused_With_A_Leading_Apostrophe(string text)
    {
        Assert.True(CsvSafeWriter.IsFormulaLike(text));
        Assert.StartsWith("'", CsvSafeWriter.Neutralise(text), StringComparison.Ordinal);
        Assert.False(CsvSafeWriter.IsFormulaLike(CsvSafeWriter.Neutralise(text)));
        var field = CsvSafeWriter.Text(text);
        Assert.False(field.TrimStart('"').StartsWith("=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("plate_w")]
    [InlineData("40.5")]
    [InlineData("a=b")]
    [InlineData("a-b")]
    [InlineData("")]
    public void An_Ordinary_Text_Is_Written_Unchanged(string text) => Assert.Equal(text, CsvSafeWriter.Neutralise(text));

    [Fact]
    public void Fields_Are_Quoted_Per_Rfc4180_And_Numbers_Are_Invariant_And_Never_Neutralised()
    {
        Assert.Equal("\"a,b\"", CsvSafeWriter.Text("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", CsvSafeWriter.Text("say \"hi\""));
        Assert.Equal("\"a\nb\"", CsvSafeWriter.Text("a\nb"));
        Assert.Equal("\"'=1,2\"", CsvSafeWriter.Text("=1,2"));
        Assert.Equal("-1.5", CsvSafeWriter.Number(-1.5));
        Assert.Equal("0.1", CsvSafeWriter.Number(0.1));
        Assert.Equal(string.Empty, CsvSafeWriter.Number(double.NaN));
        Assert.Equal(string.Empty, CsvSafeWriter.Number(double.PositiveInfinity));
        Assert.Equal("a;b\r\n", CsvSafeWriter.Row(["a", "b"], ';'));
    }

    [Fact]
    public void Written_Csv_Reads_Back_To_The_Same_Fields_Apart_From_The_Apostrophe()
    {
        var values = new[] { "plain", "with,comma", "with \"quote\"", "multi\nline", "=EVIL()" };
        var csv = CsvSafeWriter.Row(values.Select(v => CsvSafeWriter.Text(v)));
        var back = CsvReader.Read(csv).Rows[0];
        Assert.Equal(["plain", "with,comma", "with \"quote\"", "multi\nline", "'=EVIL()"], back);
    }

    // ---- streams -------------------------------------------------------

    private sealed class EndlessStream : Stream
    {
        public long Produced { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Produced += buffer.Length;
            return ValueTask.FromResult(buffer.Length);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
