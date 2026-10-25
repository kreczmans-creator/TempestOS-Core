using System.Text;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The works the worksheet templates cite, as AMA (American Medical Association, 11th edition) reference-list entries,
/// and the citing of them on an issued sheet: superscript numbers in order of first citation, each with its locator
/// (clause, section, table) in parentheses, and one numbered list at the end of the sheet (the owner's ask, 2026-10-24).
/// </summary>
/// <remarks>
/// Only bibliographic facts are held here: authors, titles, editions, publishers and years, never any text of a work.
/// A standard cited without a year in its template is an undated reference (the edition current when the sheet is
/// used), so its entry carries no year either. A book's edition is the one named here (D-44); a work not listed falls
/// back to the template's own words.
/// </remarks>
public static class CalcBibliography
{
    private static readonly (string[] Keys, string Entry)[] Works =
    [
        (["Shigley"], "Budynas RG, Nisbett JK. Shigley's Mechanical Engineering Design. 11th ed. McGraw-Hill Education; 2020."),
        (["Roark"], "Young WC, Budynas RG, Sadegh AM. Roark's Formulas for Stress and Strain. 8th ed. McGraw-Hill; 2012."),
        (["Incropera"], "Bergman TL, Lavine AS, Incropera FP, DeWitt DP. Fundamentals of Heat and Mass Transfer. 7th ed. Wiley; 2011."),
        (["Çengel & Ghajar", "Ghajar"], "Çengel YA, Ghajar AJ. Heat and Mass Transfer: Fundamentals and Applications. 5th ed. McGraw-Hill Education; 2015."),
        (["Çengel & Cimbala", "Cimbala"], "Çengel YA, Cimbala JM. Fluid Mechanics: Fundamentals and Applications. 4th ed. McGraw-Hill Education; 2018."),
        (["Hibbeler"], "Hibbeler RC. Structural Analysis. 10th ed. Pearson; 2018."),
        (["Gardner & Nethercot", "Nethercot"], "Gardner L, Nethercot DA. Designers' Guide to EN 1993-1-1: Eurocode 3: Design of Steel Structures, General Rules and Rules for Buildings. Thomas Telford; 2005."),
        (["Hughes & Drury", "Drury"], "Hughes A, Drury B. Electric Motors and Drives: Fundamentals, Types and Applications. 5th ed. Newnes; 2019."),
        (["Hughes, Electrical"], "Hughes E. Hughes Electrical and Electronic Technology. 12th ed. Pearson; 2016."),
        (["Kell"], "Kell GS. Density, thermal expansivity, and compressibility of liquid water from 0° to 150°C: correlations and tables for atmospheric pressure and saturation reviewed and expressed on 1968 temperature scale. J Chem Eng Data. 1975;20(1):97-105."),
        (["Hyland"], "Hyland RW, Wexler A. Formulations for the thermodynamic properties of the saturated phases of H2O from 173.15 K to 473.15 K. ASHRAE Trans. 1983;89(2A):500-519."),
        (["ASHRAE Handbook"], "American Society of Heating, Refrigerating and Air-Conditioning Engineers. ASHRAE Handbook—Fundamentals. ASHRAE."),
        (["CIBSE Guide C"], "Chartered Institution of Building Services Engineers. CIBSE Guide C: Reference Data. CIBSE; 2007."),
        (["Crane"], "Crane Co. Flow of Fluids Through Valves, Fittings and Pipe. Technical Paper No. 410. Crane Co."),
        (["AISC Steel Construction Manual"], "American Institute of Steel Construction. Steel Construction Manual. 15th ed. AISC; 2017."),
        (["AISC Design Guide 9", "Seaburg"], "Seaburg PA, Carter CJ. Torsional Analysis of Structural Steel Members. Steel Design Guide 9. American Institute of Steel Construction; 1997."),
        (["ASME BPVC", "ASME VIII"], "American Society of Mechanical Engineers. Boiler and Pressure Vessel Code, Section VIII, Division 1: Rules for Construction of Pressure Vessels. ASME."),
        (["ASME BTH-1"], "American Society of Mechanical Engineers. Design of Below-the-Hook Lifting Devices. ASME BTH-1. ASME."),
        (["ASME Y14.5"], "American Society of Mechanical Engineers. Dimensioning and Tolerancing. ASME Y14.5. ASME."),
        (["VDI 2230"], "Verein Deutscher Ingenieure. Systematic Calculation of Highly Stressed Bolted Joints: Joints With One Cylindrical Bolt. VDI 2230 Part 1:2015. Beuth Verlag; 2015."),
        (["ISO 898-1"], "International Organization for Standardization. Mechanical Properties of Fasteners Made of Carbon Steel and Alloy Steel—Part 1: Bolts, Screws and Studs With Specified Property Classes—Coarse Thread and Fine Pitch Thread. ISO 898-1:2013. ISO; 2013."),
        (["ISO 286-1"], "International Organization for Standardization. Geometrical Product Specifications (GPS)—ISO Code System for Tolerances on Linear Sizes—Part 1: Basis of Tolerances, Deviations and Fits. ISO 286-1:2010. ISO; 2010."),
        (["ISO 286-2"], "International Organization for Standardization. Geometrical Product Specifications (GPS)—ISO Code System for Tolerances on Linear Sizes—Part 2: Tables of Standard Tolerance Classes and Limit Deviations for Holes and Shafts. ISO 286-2:2010. ISO; 2010."),
        (["ISO 281"], "International Organization for Standardization. Rolling Bearings—Dynamic Load Ratings and Rating Life. ISO 281. ISO."),
        (["ISO 2768-1"], "International Organization for Standardization. General Tolerances—Part 1: Tolerances for Linear and Angular Dimensions Without Individual Tolerance Indications. ISO 2768-1. ISO."),
        (["JEDEC JESD51-1"], "JEDEC Solid State Technology Association. Integrated Circuits Thermal Measurement Method—Electrical Test Method (Single Semiconductor Device). JESD51-1. JEDEC."),
        (["IEC TR 60890"], "International Electrotechnical Commission. A Method of Temperature-Rise Verification of Low-Voltage Switchgear and Controlgear Assemblies by Calculation. IEC TR 60890. IEC."),
        (["EN 1990"], "British Standards Institution. Eurocode: Basis of Structural Design. BS EN 1990. BSI."),
        (["EN 1993-1-1"], "British Standards Institution. Eurocode 3: Design of Steel Structures—Part 1-1: General Rules and Rules for Buildings. BS EN 1993-1-1. BSI."),
        (["EN 1993-1-8"], "British Standards Institution. Eurocode 3: Design of Steel Structures—Part 1-8: Design of Joints. BS EN 1993-1-8:2005. BSI; 2005."),
        (["EN 1993-1-9"], "British Standards Institution. Eurocode 3: Design of Steel Structures—Part 1-9: Fatigue. BS EN 1993-1-9. BSI."),
        (["BS 5950-1"], "British Standards Institution. Structural Use of Steelwork in Building—Part 1: Code of Practice for Design—Rolled and Welded Sections. BS 5950-1:2000. BSI; 2000."),
        (["BS 7671"], "British Standards Institution; Institution of Engineering and Technology. Requirements for Electrical Installations: IET Wiring Regulations. BS 7671. IET."),
        (["BS 8888"], "British Standards Institution. Technical Product Documentation and Specification. BS 8888. BSI."),
        (["EN 10210-2"], "British Standards Institution. Hot Finished Steel Structural Hollow Sections—Part 2: Tolerances, Dimensions and Sectional Properties. BS EN 10210-2. BSI."),
        (["EN 10365"], "British Standards Institution. Hot Rolled Steel Channels, I and H Sections—Dimensions and Masses. BS EN 10365. BSI."),
        (["EN 1505"], "British Standards Institution. Ventilation for Buildings—Sheet Metal Air Ducts and Fittings With Rectangular Cross-Section—Dimensions. BS EN 1505. BSI."),
        (["EN 1506"], "British Standards Institution. Ventilation for Buildings—Sheet Metal Air Ducts and Fittings With Circular Cross-Section—Dimensions. BS EN 1506. BSI."),
        (["EN 61439-1"], "British Standards Institution. Low-Voltage Switchgear and Controlgear Assemblies—Part 1: General Rules. BS EN 61439-1. BSI."),
        (["EN ISO 6946"], "British Standards Institution. Building Components and Building Elements—Thermal Resistance and Thermal Transmittance—Calculation Methods. BS EN ISO 6946. BSI."),
        (["servo-sizing"], "Kollmorgen; Parker Hannifin. Servo motor sizing guides [manufacturer technical literature]."),
        (["enclosure climate-control"], "Rittal; nVent Hoffman. Enclosure climate control guides [manufacturer technical literature]."),
    ];

    /// <summary>One numbered entry of a sheet's reference list.</summary>
    public sealed record Reference(int Number, string Entry);

    /// <summary>A citation: the reference's number and the locator within it (a clause, section or table), if any.</summary>
    public sealed record Citation(int Number, string? Locator);

    /// <summary>The AMA entry for a work as a template names it ("Shigley's Mechanical Engineering Design", "BS EN 1993-1-8").</summary>
    public static string EntryFor(string work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Find(work) ?? $"{work.TrimEnd('.')}.";
    }

    private static string? Find(string text)
    {
        foreach (var (keys, entry) in Works)
        {
            if (keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return entry;
        }

        return null;
    }

    /// <summary>A number as AMA sets it, in superscript ("12" → "¹²").</summary>
    public static string Superscript(int number)
    {
        const string Digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = new StringBuilder();

        foreach (var c in number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            text.Append(Digits[c - '0']);

        return text.ToString();
    }

    /// <summary>A citation as printed: the superscript number, then its locator in parentheses ("¹(R8)").</summary>
    public static string Format(Citation citation)
    {
        ArgumentNullException.ThrowIfNull(citation);
        return Superscript(citation.Number) + (citation.Locator is { Length: > 0 } l ? $"({l})" : string.Empty);
    }

    /// <summary>
    /// Numbers a sheet's references in order of first citation. The method's references are cited first (in the order
    /// the template lists them), then any work a step or check names that the method does not.
    /// </summary>
    public sealed class Register
    {
        private readonly List<Reference> _references = [];
        private readonly IReadOnlyList<CalcClauseRef> _clauseRefs;

        public Register(IReadOnlyList<CalcClauseRef> clauseRefs)
        {
            ArgumentNullException.ThrowIfNull(clauseRefs);
            _clauseRefs = clauseRefs;
        }

        /// <summary>The numbered list, in order of first citation.</summary>
        public IReadOnlyList<Reference> References => _references;

        /// <summary>Cites one of the method's own clause references.</summary>
        public Citation Cite(CalcClauseRef reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            return new Citation(NumberOf(EntryFor(reference.Standard)), reference.Clause is { Length: > 0 } and not "—" ? reference.Clause : null);
        }

        /// <summary>
        /// Cites the work a step or check names ("VDI 2230 R8", "Shigley §7-4", or a bare clause "6.2.5" of the method's
        /// own standard), with the rest of the text as the locator.
        /// </summary>
        public Citation Cite(string clause)
        {
            ArgumentNullException.ThrowIfNull(clause);
            var text = clause.Trim();

            // A bare clause number belongs to the method's first standard that is cited by clause number.
            if (text.Length > 0 && char.IsDigit(text[0]) && _clauseRefs.FirstOrDefault(r => r.Clause.Length > 0 && char.IsDigit(r.Clause[0])) is { } own)
                return new Citation(NumberOf(EntryFor(own.Standard)), text);

            var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // The longest leading run of tokens that names one of the method's works, or a known work.
            for (var take = tokens.Length; take >= 1; take--)
            {
                var locator = take < tokens.Length ? string.Join(' ', tokens[take..]) : null;
                var match = _clauseRefs.Where(r => Names(r.Standard, tokens[..take])).ToList();

                if (match.Count > 0)
                {
                    // Two works by one author (Çengel): the one whose clause the locator names, else the first.
                    var chosen = match.FirstOrDefault(r => locator is not null && r.Clause.Contains(locator, StringComparison.Ordinal)) ?? match[0];
                    return new Citation(NumberOf(EntryFor(chosen.Standard)), locator);
                }
            }

            // A work the method does not list: the shortest leading run the registry knows, the rest the locator.
            for (var take = 1; take <= tokens.Length; take++)
            {
                if (Find(string.Join(' ', tokens[..take])) is { } entry)
                    return new Citation(NumberOf(entry), take < tokens.Length ? string.Join(' ', tokens[take..]) : null);
            }

            return new Citation(NumberOf(EntryFor(tokens.Length > 1 ? string.Join(' ', tokens[..^1]) : text)), tokens.Length > 1 ? tokens[^1] : null);
        }

        private int NumberOf(string entry)
        {
            if (_references.FirstOrDefault(r => r.Entry == entry) is { } known)
                return known.Number;

            var next = new Reference(_references.Count + 1, entry);
            _references.Add(next);
            return next.Number;
        }

        /// <summary>Whether every token names part of <paramref name="standard"/> ("Shigley" in "Shigley's …", "1993-1-8" in "EN 1993-1-8:2005").</summary>
        private static bool Names(string standard, string[] tokens)
        {
            var words = standard.Split([' ', ',', ':', '\'', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
            return tokens.All(t => t.Length > 1 && words.Any(w => string.Equals(w, t.TrimEnd(',', '.'), StringComparison.OrdinalIgnoreCase)));
        }
    }
}
