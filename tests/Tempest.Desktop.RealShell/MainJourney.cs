using System.Globalization;
using System.Security.Cryptography;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// The acceptance journey itself: the product's own value chain, driven
/// through the real shell with real operating-system input, in the order
/// `PHYSICAL_REVIEW.md` §7–§7j walks it.
///
/// <para>
/// Steps marked <c>required: false</c> are of two kinds, both named in the
/// report: representative extras, and steps that are pinned to an open
/// defect and are *expected* to fail until it is fixed. A required step
/// that fails or cannot be read fails the whole run.
/// </para>
/// </summary>
internal static class MainJourney
{
    private const string ProjectName = "Apollo Pump Redesign";
    private const string ClientName = "Northwind Marine Ltd";
    private const string CalculationName = "Apollo discharge beam check";

    internal static void Run(Journal journal)
    {
        var evidenceFile = WriteSampleEvidenceFile(journal.OutputDirectory);
        var exportPath = Path.Combine(journal.OutputDirectory, "Q-2026-001-quote.pdf");

        Home(journal);
        RailNavigation(journal);
        ReferenceData(journal);
        NewProject(journal);
        Quotation(journal);
        QuoteOutputs(journal, exportPath);
        Calculators(journal);
        Evidence(journal, evidenceFile);
        Delivery(journal);
        Finance(journal);
        Representative(journal);
        CloseThroughRealInput(journal);
    }

    // ==================================================================
    // The shell
    // ==================================================================

    private static void Home(Journal journal)
    {
        journal.Step(
            "home", "Read the Home dashboard on a fresh persistence root", "none (first frame)",
            "Five task tiles at 0, an honest commercial snapshot, and the right rail's own empty states",
            () =>
            {
                var tiles = new[] { "Overdue: 0", "Due today: 0", "Due this week: 0", "Approvals: 0", "Finance: 0" };
                var missing = tiles.Where(tile => Ui.ByNameContaining(tile) is null).ToList();
                var snapshot = Ui.FirstTextContaining("Invoices: unavailable") ?? "(no commercial snapshot)";
                var continueRail = Ui.FirstTextContaining("No projects yet") ?? "(no Continue text)";

                return missing.Count == 0 && snapshot.Contains("No accounts reading yet", StringComparison.Ordinal)
                    ? Act.Verified($"all five tiles read 0; \"{snapshot}\"; Continue reads \"{continueRail}\"")
                    : Act.Failed($"missing tiles: {string.Join(", ", missing)}; snapshot \"{snapshot}\"");
            });
    }

    private static void RailNavigation(Journal journal)
    {
        journal.Step(
            "rail", "Click every rail entry — Home, Projects, Tasks, Engineering, Business", "mouse (xdotool click)",
            "Each click lands on that area and the status bar's own Location segment says so",
            () =>
            {
                var seen = new List<string>();
                foreach (var area in new[] { "Projects", "Tasks", "Engineering", "Business", "Home" })
                {
                    if (!Act.Click(area, settleMs: 900))
                        return Act.Failed($"the rail has no clickable '{area}' ({Act.LastProblem})");

                    if (!Ui.WaitUntil(() => string.Equals(Ui.ByName("Location")?.Text, area, StringComparison.Ordinal), 8_000))
                        return Act.Failed($"clicking '{area}' left Location reading '{Ui.ByName("Location")?.Text}'");

                    seen.Add(area);
                }

                return Act.Verified($"Location followed the rail through {string.Join(" -> ", seen)}");
            });
    }

    // ==================================================================
    // Reference data - the governed records the rest of the chain pins
    // ==================================================================

    private static void ReferenceData(Journal journal)
    {
        journal.Step(
            "refdata", "Engineering → Reference data", "mouse",
            "Every library has its own heading, and the seeded records list as Draft",
            () =>
            {
                if (!Act.Click("Engineering", settleMs: 900) || !Act.ClickRow("Reference data", settleMs: 2_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Release mat-s355j2") is not null, 20_000))
                    return Act.Failed("the Materials library never listed mat-s355j2");

                var rateCards = Ui.FirstTextContaining("Rate cards (") ?? "(no Rate cards heading)";
                var people = Ui.FirstTextContaining("People (") ?? "(no People heading)";
                return Act.Verified($"libraries listed; \"{rateCards}\"; \"{people}\"");
            });

        journal.Step(
            "release-material", "Release the seeded S355J2 material", "mouse",
            "The record walks Draft → Released in one act and the status bar says so",
            () =>
            {
                if (!Act.Click("Release mat-s355j2", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ShowsText("Released 'mat-s355j2'"), 20_000))
                    return Act.Failed($"nothing reported the release; the status bar reads \"{Act.Status()}\"");

                var row = Ui.FirstTextContaining("mat-s355j2 — S355J2") ?? "(row not found)";
                return row.Contains("Released", StringComparison.Ordinal)
                    ? Act.Verified($"status \"{Act.Status()}\"; row reads \"{Trim(row)}\"")
                    : Act.Failed($"the row still reads \"{Trim(row)}\"");
            });

        journal.Step(
            "add-person", "Add a second person to the People library and release them", "keyboard + mouse",
            "The person is registered from the library's own form, opens right up, and releases",
            () =>
            {
                if (!Act.TypeInto("Display name", "Dana Whitfield")
                    || !Act.TypeInto("Role", "Principal Engineer")
                    || !Act.TypeInto("Email", "dana@tempest-engineering.co.uk"))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Add Person", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ShowsText("Added person 'Dana Whitfield'"), 20_000))
                    return Act.Failed($"nothing reported the person; the status bar reads \"{Act.Status()}\"");

                if (!Act.Click("Release person-dana-whitfield", settleMs: 2_500))
                    return Act.Failed($"the new person's own Release button was not reachable ({Act.LastProblem})");

                return Ui.WaitUntil(() => Ui.ShowsText("Released 'person-dana-whitfield'"), 20_000)
                    ? Act.Verified($"status \"{Act.Status()}\"")
                    : Act.Failed($"the status bar reads \"{Act.Status()}\"");
            });

        // The consultancy's own rates. Until 2026-09-16 nothing in the shipped
        // application could create a rate card, so on a clean root the
        // New Project prompt offered none, no timesheet entry could be
        // priced and no invoice request raised (this journey's own
        // raise-invoice step pinned that defect). The Libraries area's
        // "Add a rate card" form closes it; this step drives it the way a
        // user would and releases the card from its own row.
        journal.Step(
            "add-rate-card", "Add the consultancy's rate card to the Rate cards library and release it", "keyboard + mouse",
            "One graded hourly rate is registered from the library's own form, opens right up, and releases",
            () =>
            {
                if (!Act.TypeInto("Rate card name", "Consultancy standard rates")
                    || !Act.TypeInto("Rate card grade", "Engineer")
                    || !Act.TypeInto("Rate card hourly rate", "95"))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Add Rate Card", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ShowsText("Added rate card 'Consultancy standard rates'"), 20_000))
                    return Act.Failed($"nothing reported the rate card; the status bar reads \"{Act.Status()}\"");

                if (!Act.Click("Release ratecard-consultancy-standard-rates", settleMs: 2_500))
                    return Act.Failed($"the new rate card's own Release button was not reachable ({Act.LastProblem})");

                return Ui.WaitUntil(() => Ui.ShowsText("Released 'ratecard-consultancy-standard-rates'"), 20_000)
                    ? Act.Verified($"status \"{Act.Status()}\"")
                    : Act.Failed($"the status bar reads \"{Act.Status()}\"");
            });
    }

    // ==================================================================
    // Client and project context
    // ==================================================================

    private static void NewProject(Journal journal)
    {
        journal.Step(
            "new-project-prompt", "Projects → Open → New Project…", "mouse",
            "The prompt is pre-filled with the next free identifier P-0001 and offers Client, Rate card, PO reference",
            () =>
            {
                if (!Act.Click("Projects", settleMs: 900) || !Act.ClickRow("Open", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("New Project…", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Name") is not null && Ui.ByName("Client") is not null, 10_000))
                    return Act.Failed("the New Project prompt never appeared");

                var heading = Ui.FirstTextContaining("Name for P-") ?? "(no identifier line)";
                var rateCard = Ui.ByName("Rate card")?.Text ?? "(none)";
                var warning = Ui.FirstTextContaining("Time cannot be recorded") ?? string.Empty;
                return Act.Verified($"\"{heading}\"; Rate card reads '{rateCard}'; inline note \"{Trim(warning)}\"");
            });

        journal.Step(
            "client-picker", "Client → Add organisation… → register the client and choose it", "mouse + keyboard",
            "The organisation picker opens over the prompt, registers Northwind Marine Ltd and hands it back as the client",
            () =>
            {
                if (!Act.Choose("Client", "Add organisation"))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("New organisation name") is not null, 8_000))
                    return Act.Failed("the organisation picker never appeared");

                // The picker must be usable, not merely present: a control
                // that the prompt above it covers cannot be clicked at all.
                if (!Act.TypeInto("Reference", "ORG-NWM") || !Act.TypeInto("New organisation name", ClientName))
                    return Act.Failed($"the picker's own fields could not be typed into — {Act.LastProblem}");

                if (!Act.Click("Add organisation", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText($"{ClientName} (ORG-NWM)", 8_000))
                    return Act.Failed("the registered organisation did not appear in the picker's own list");

                if (!Act.Click($"{ClientName} (ORG-NWM)", settleMs: 800) || !Act.Click("Choose", settleMs: 1_200))
                    return Act.Failed(Act.LastProblem);

                var client = Ui.ByName("Client")?.Text ?? string.Empty;
                return client.Contains(ClientName, StringComparison.Ordinal)
                    ? Act.Verified($"Client now reads '{client}'")
                    : Act.Failed($"Client reads '{client}'");
            });

        journal.Step(
            "create-project", "Name the project, give it a PO reference, and accept", "keyboard + mouse",
            "The project is created and opens right up on its Quote tab carrying one Draft quotation",
            () =>
            {
                if (!Act.TypeInto("Name", ProjectName) || !Act.TypeInto("PO reference", "PO-1001"))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("OK", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Quote") is not null && Ui.ByNameContaining("Send Q-") is not null, 25_000))
                    return Act.Failed("the project did not open on its Quote tab with a quotation");

                var identity = Ui.FirstTextContaining("Q-2026-") ?? "(no quotation identity)";
                var project = Ui.ByName("Current project")?.Text ?? "(none)";
                return identity.Contains("Draft", StringComparison.Ordinal)
                    ? Act.Verified($"status bar names '{project}'; quote reads \"{identity}\"")
                    : Act.Failed($"quote reads \"{identity}\"");
            });

        journal.Step(
            "project-details", "The project's own Details tab", "mouse",
            "Client, PO reference and rate card are shown by name, not as an id",
            () =>
            {
                if (!Act.Click("Details", settleMs: 2_000))
                    return Act.Failed(Act.LastProblem);

                var client = Ui.FirstTextContaining(ClientName);
                var purchaseOrder = Ui.FirstTextContaining("PO-1001");
                return client is not null && purchaseOrder is not null
                    ? Act.Verified($"Details reads \"{Trim(client)}\" and \"{Trim(purchaseOrder)}\"")
                    : Act.Failed($"client: {client ?? "missing"}; PO: {purchaseOrder ?? "missing"}");
            });
    }

    // ==================================================================
    // Quotation
    // ==================================================================

    private static void Quotation(Journal journal)
    {
        journal.Step(
            "quote-lines", "Quote tab → one hourly line and one fixed-price line", "keyboard + mouse",
            "Each line lists with its own basis and the Total is their sum",
            () =>
            {
                if (!Act.Click("Quote", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.TypeInto("Line description", "Concept design and pump sizing")
                    || !Act.TypeInto("Line hours", "40")
                    || !Act.TypeInto("Line rate", "120")
                    || !Act.Click("Add line", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.TypeInto("Line description", "Detailed calculation pack")
                    || !Act.TypeInto("Line fixed price", "2500")
                    || !Act.Click("Add line", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                var hourly = Ui.FirstTextContaining("Concept design and pump sizing  •") ?? "(hourly line missing)";
                var fixedPrice = Ui.FirstTextContaining("Detailed calculation pack  •") ?? "(fixed line missing)";
                var total = Ui.FirstTextContaining("Total £") ?? "(no total)";

                return total.Contains("7,300.00", StringComparison.Ordinal)
                    ? Act.Verified($"\"{hourly}\"; \"{fixedPrice}\"; \"{total}\"")
                    : Act.Failed($"\"{hourly}\"; \"{fixedPrice}\"; total reads \"{total}\"");
            });

        journal.Step(
            "quote-send", "Send the quotation (confirmed)", "mouse",
            "A confirmation is asked for, then the quotation reads Sent",
            () =>
            {
                if (!Act.Click("Send Q-2026-001", settleMs: 1_200))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Confirm("Send the selected quotation?"))
                    return Act.Failed(Act.LastProblem);

                return Ui.WaitForText("Q-2026-001 — Sent", 25_000)
                    ? Act.Verified($"quote reads \"{Ui.FirstTextContaining("Q-2026-001 —")}\"; status \"{Act.Status()}\"")
                    : Act.Failed($"quote reads \"{Ui.FirstTextContaining("Q-2026-001 —")}\"");
            });

        journal.Step(
            "quote-accept", "Accept the quotation (confirmed)", "mouse",
            "One Deliverable and one Requirement per line, under a milestone named after the quotation",
            () =>
            {
                if (!Act.Click("Accept Q-2026-001", settleMs: 1_200))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Confirm("Accept the selected quotation?"))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText("Created on acceptance", 25_000))
                    return Act.Failed("the Quote tab grew no \"Created on acceptance\" section");

                var snapshot = Ui.Snapshot();
                var deliverables = snapshot.Where(node => node.Name.StartsWith("Open deliverable", StringComparison.Ordinal)).Select(node => node.Name).Distinct(StringComparer.Ordinal).Count();
                var requirements = snapshot.Where(node => node.Name.StartsWith("Open requirement", StringComparison.Ordinal)).Select(node => node.Name).Distinct(StringComparer.Ordinal).Count();

                return deliverables == 2 && requirements == 2
                    ? Act.Verified($"status \"{Act.Status()}\"; {deliverables} Open deliverable and {requirements} Open requirement buttons")
                    : Act.Failed($"{deliverables} Open deliverable / {requirements} Open requirement buttons; status \"{Act.Status()}\"");
            });
    }

    private static void QuoteOutputs(Journal journal, string exportPath)
    {
        journal.Step(
            "quote-export", "Export the quotation sheet through the OS save dialog", "mouse + keyboard, including the GTK dialog",
            "A real PDF is written where the save dialog was pointed",
            () =>
            {
                if (!Act.Click("Export Q-2026-001", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!OsInput.DriveSaveDialog("Export Q-2026-001", exportPath))
                    return Act.Unknown("the operating system's own save dialog never appeared");

                if (!Ui.WaitUntil(() => File.Exists(exportPath) && new FileInfo(exportPath).Length > 1_000, 20_000))
                    return Act.Failed($"nothing was written to '{exportPath}'");

                var header = new byte[5];
                using (var stream = File.OpenRead(exportPath))
                    _ = stream.Read(header, 0, header.Length);

                var isPdf = System.Text.Encoding.ASCII.GetString(header).StartsWith("%PDF-", StringComparison.Ordinal);
                var size = new FileInfo(exportPath).Length;

                return isPdf
                    ? Act.Verified($"'{Path.GetFileName(exportPath)}' written, {size:N0} bytes, and it really is a PDF (%PDF- header)")
                    : Act.Failed($"'{Path.GetFileName(exportPath)}' is {size:N0} bytes but does not start with %PDF-");
            },
            required: false);

        journal.Step(
            "accept-stale-tabs", "Straight after acceptance, look at the Deliverables tab", "mouse",
            "It lists the two deliverables the acceptance just created (PHYSICAL_REVIEW §7c D7)",
            () =>
            {
                if (!Act.Click("Deliverables", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                Thread.Sleep(1_200);

                // The Deliverables tab's own summary line, not the status
                // bar's report of the acceptance - they both say
                // "deliverable(s)" and only one of them is the claim here.
                var text = Ui.VisibleText().FirstOrDefault(value =>
                               value.Contains("deliverable(s),", StringComparison.Ordinal)
                               || value.StartsWith("No deliverables", StringComparison.Ordinal))
                           ?? "(the tab showed no summary line at all)";

                return text.Contains("No deliverables", StringComparison.Ordinal)
                    ? Act.Failed($"DEFECT: the tab reads \"{text}\" although the acceptance reported two deliverables")
                    : Act.Verified($"the tab reads \"{text}\"");
            },
            required: false);

        journal.Step(
            "reopen-project", "Leave the project and open it again", "mouse",
            "The Deliverables tab now lists both deliverables, and Requirements lists both requirements",
            () =>
            {
                if (!Act.Click("Home", settleMs: 900) || !Act.Click("Projects", settleMs: 1_200))
                    return Act.Failed(Act.LastProblem);

                if (!Act.ClickRow("Open", settleMs: 1_200) || !Act.ClickRow($"P-0001 {ProjectName}", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Deliverables", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.VisibleText().Any(value => value.Contains("deliverable(s),", StringComparison.Ordinal)), 15_000))
                    return Act.Failed($"the tab reads \"{Ui.VisibleText().FirstOrDefault(value => value.StartsWith("No deliverables", StringComparison.Ordinal)) ?? "(no summary line)"}\"");

                var deliverables = Ui.VisibleText().First(value => value.Contains("deliverable(s),", StringComparison.Ordinal));

                if (!Act.Click("Requirements", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                var requirements = Ui.FirstTextContaining("requirement(s) in") ?? Ui.FirstTextContaining("No requirement") ?? "(nothing)";
                return Act.Verified($"Deliverables: \"{deliverables}\"; Requirements: \"{Trim(requirements)}\"");
            });
    }

    // ==================================================================
    // The engineering calculation
    // ==================================================================

    private static void Calculators(Journal journal)
    {
        journal.Step(
            "calculators", "Engineering → Calculators", "mouse",
            "A catalogue of sixteen calculations by category, and the Libraries panel counting released records",
            () =>
            {
                if (!Act.Click("Engineering", settleMs: 900) || !Act.ClickRow("Calculators", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Calculator catalogue") is not null, 20_000))
                    return Act.Failed("the calculator catalogue never rendered");

                // "Calculator catalogue" and "Calculator status" are the
                // panel and the status line, not calculations.
                var count = Ui.Snapshot()
                    .Where(node => node.TypeName == "TreeViewItem" && node.Name.StartsWith("Calculator ", StringComparison.Ordinal))
                    .Select(node => node.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var libraries = Ui.FirstTextContaining("Materials:") ?? "(no library counts)";
                return count == 16
                    ? Act.Verified($"{count} calculations listed; libraries read \"{libraries}\"")
                    : Act.Failed($"{count} calculations listed (expected 16); libraries read \"{libraries}\"");
            });

        journal.Step(
            "calculator-form", "Pick Beam bending and deflection and its governed material", "mouse",
            "The form is generated with a unit picker per quantity; the material's own properties fill the sourced fields",
            () =>
            {
                if (!Act.Click("Calculator Beam bending and deflection", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Material record") is not null && Ui.ByName("Span L") is not null, 15_000))
                    return Act.Failed("the generated form never appeared");

                if (!Act.Choose("Material record", "S355J2"))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Young's modulus E")?.Text == "210", 12_000))
                    return Act.Failed($"Young's modulus reads '{Ui.ByName("Young's modulus E")?.Text}'");

                var status = Ui.ByName("Calculator status")?.Text ?? string.Empty;
                var allowable = Ui.ByName("Allowable bending stress")?.Text ?? string.Empty;
                var method = Ui.FirstTextContaining("Method: Euler-Bernoulli") ?? "(no method reference)";
                var specification = Ui.FirstTextContaining("Specification: docs/engineering") ?? "(no specification path)";

                return status.Contains("mat-s355j2@5", StringComparison.Ordinal) && allowable == "355"
                    ? Act.Verified($"E=210 GPa, allowable={allowable} MPa; \"{status}\"; \"{method}\"; \"{specification}\"")
                    : Act.Failed($"E={Ui.ByName("Young's modulus E")?.Text}, allowable={allowable}; status \"{status}\"");
            });

        journal.Step(
            "calculate", "Enter the inputs and press Calculate", "keyboard + mouse",
            "Deflection 3.96825 mm, bending stress 125 MPa, \"Meets its criteria\" (PHYSICAL_REVIEW §7i C4)",
            () =>
            {
                if (!Act.TypeInto("Load W", "10")
                    || !Act.TypeInto("Span L", "2000")
                    || !Act.TypeInto("Second moment of area I", "2000000")
                    || !Act.TypeInto("Extreme fibre distance c", "50")
                    || !Act.TypeInto("Deflection limit", "8")
                    || !Act.TypeInto("Calculation name", CalculationName))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Calculate", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => (Ui.ByName("Calculator status")?.Text ?? string.Empty).Contains("Calculated", StringComparison.Ordinal), 25_000))
                    return Act.Failed($"the calculator status reads \"{Ui.ByName("Calculator status")?.Text}\"");

                var outcome = Ui.ByName("Calculation outcome")?.Text ?? "(no outcome)";
                var deflection = Ui.FirstTextContaining("3.96825 mm");
                var stress = Ui.FirstTextContaining("125 MPa");

                return outcome.Contains("Meets its criteria", StringComparison.Ordinal) && deflection is not null && stress is not null
                    ? Act.Verified($"\"{outcome}\"; deflection {deflection}; stress {stress}; status \"{Ui.ByName("Calculator status")?.Text}\"")
                    : Act.Failed($"outcome \"{outcome}\"; deflection {deflection ?? "not shown"}; stress {stress ?? "not shown"}");
            });

        journal.Step(
            "recalculate", "Change the span to 2500 mm and calculate again", "keyboard + mouse",
            "A second run on the same calculation, with a deflection of 7.7505 mm",
            () =>
            {
                if (!Act.TypeInto("Span L", "2500") || !Act.Click("Calculate", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => (Ui.ByName("Calculator status")?.Text ?? string.Empty).Contains("again", StringComparison.Ordinal), 25_000))
                    return Act.Failed($"the calculator status reads \"{Ui.ByName("Calculator status")?.Text}\"");

                var deflection = Ui.FirstTextContaining("7.7505 mm");
                return deflection is not null
                    ? Act.Verified($"status \"{Ui.ByName("Calculator status")?.Text}\"; {Trim(deflection)}")
                    : Act.Failed($"no 7.7505 mm reading; status \"{Ui.ByName("Calculator status")?.Text}\"");
            });

        journal.Step(
            "compare", "Compare with previous", "mouse",
            "A Before/After table naming only what changed between the last two runs",
            () =>
            {
                if (!Act.Click("Compare with previous", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.FirstTextContaining("Compared with the previous run") is not null, 15_000))
                    return Act.Failed($"the calculator status reads \"{Ui.ByName("Calculator status")?.Text}\"");

                var line = Ui.FirstTextContaining("Compared with the previous run")!;
                var section = Ui.FirstTextContaining("Comparison with the previous run") ?? "(no comparison section)";
                var spans = Ui.VisibleText()
                    .Where(text => text.Contains("2000", StringComparison.Ordinal) && text.Contains("2500", StringComparison.Ordinal))
                    .Select(Trim)
                    .ToList();

                return Act.Verified(
                    $"\"{Trim(line)}\"; section \"{section}\""
                    + (spans.Count > 0 ? $"; before/after row(s): {string.Join(" ; ", spans)}" : string.Empty));
            },
            required: false);

        journal.Step(
            "calculation-register", "Engineering → Engineering Calculations", "mouse",
            "The named calculation is listed against its own project",
            () =>
            {
                if (!Act.ClickRow("Engineering Calculations", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText(CalculationName, 20_000))
                    return Act.Failed($"'{CalculationName}' is not listed");

                var row = Ui.FirstTextContaining(CalculationName)!;
                return row.Contains(ProjectName, StringComparison.Ordinal)
                    ? Act.Verified($"\"{Trim(row)}\"")
                    : Act.Failed($"\"{Trim(row)}\" does not name its project");
            });
    }

    // ==================================================================
    // Evidence - including the operating system's own file dialog
    // ==================================================================

    private static void Evidence(Journal journal, string evidenceFile)
    {
        journal.Step(
            "evidence-create", "Project → Evidence → Create, picking a real file through the OS file dialog", "mouse + keyboard, including the GTK dialog",
            "A real file is attached with its real size and SHA-256, and the record opens right up",
            () =>
            {
                if (!Act.Click("Projects", settleMs: 1_200) || !Act.ClickRow("Open", settleMs: 1_200) || !Act.ClickRow($"P-0001 {ProjectName}", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Evidence", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Create", settleMs: 2_000))
                    return Act.Failed(Act.LastProblem);

                if (!OsInput.DriveFileDialog("Pick the files this evidence records", evidenceFile))
                    return Act.Unknown("the operating system's own file dialog never appeared, so no file could be picked");

                if (!Ui.WaitUntil(() => Ui.ByName("OK") is not null, 15_000))
                    return Act.Unknown("no classification prompt followed the file dialog");

                if (!Act.Click("OK", settleMs: 2_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("No subject") is not null, 12_000))
                    return Act.Unknown("no subject prompt followed the classification");

                if (!Act.Click("No subject", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ShowsText("Created Evidence"), 25_000))
                    return Act.Failed($"nothing reported the record; the status bar reads \"{Act.Status()}\"");

                var report = Ui.FirstTextContaining("Created Evidence")!;

                var expected = Sha256(evidenceFile);
                var shown = Ui.FirstTextContaining("sha256 ") ?? "(no hash shown)";
                var size = new FileInfo(evidenceFile).Length;

                return shown.Contains(expected, StringComparison.OrdinalIgnoreCase) && shown.Contains(size.ToString("N0", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    ? Act.Verified($"\"{report}\"; attachment reads \"{Trim(shown)}\" — the hash and size match the file on disk exactly")
                    : Act.Failed($"attachment reads \"{Trim(shown)}\"; the file on disk is {size} bytes, sha256 {expected}");
            });

        journal.Step(
            "evidence-listed", "Return to the Evidence tab", "mouse",
            "The record is listed with its classification and Draft state",
            () =>
            {
                if (!Act.Click("Details", settleMs: 1_200) || !Act.Click("Evidence", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                var row = Ui.FirstTextContaining("evidence-sample");
                return row is not null
                    ? Act.Verified($"\"{Trim(row)}\"")
                    : Act.Failed("the Evidence tab lists no record");
            });
    }

    // ==================================================================
    // Delivery and finance
    // ==================================================================

    private static void Delivery(Journal journal)
    {
        journal.Step(
            "complete-deliverable", "Deliverables → Complete, with a fixed price", "mouse + keyboard",
            "The completion is recorded against the deliverable and opens right up",
            () =>
            {
                if (!Act.Click("Deliverables", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Complete Detailed calculation pack", settleMs: 2_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Fixed price") is not null, 10_000))
                    return Act.Failed("the completion prompt never appeared");

                if (!Act.TypeInto("Fixed price", "2500 GBP") || !Act.Click("Complete", settleMs: 3_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ShowsText("Deliverable completed"), 25_000))
                    return Act.Failed($"nothing reported the completion; the status bar reads \"{Act.Status()}\"");

                var completionReport = Ui.FirstTextContaining("Deliverable completed")!;

                var content = Ui.VisibleText().FirstOrDefault(text => text.StartsWith("Deliverable '", StringComparison.Ordinal));
                var note = content is null ? string.Empty
                    : content.Contains('-', StringComparison.Ordinal) && content.Length > 50
                        ? $" — but the completion's own content names the deliverable by id: \"{Trim(content)}\""
                        : string.Empty;

                return Act.Verified($"\"{Trim(completionReport)}\"{note}");
            });
    }

    private static void Finance(Journal journal)
    {
        journal.Step(
            "invoices-available", "Business → Invoices", "mouse",
            "The completion is grouped under \"Available to invoice\" with its own Raise invoice action",
            () =>
            {
                if (!Act.Click("Business", settleMs: 1_200) || !Act.ClickRow("Invoices", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText("Available to invoice", 20_000))
                    return Act.Failed("the Invoices area never grouped anything");

                var groups = new[] { "New (", "Available to invoice (", "Sent (", "Outstanding / Overdue (", "Closed" }
                    .Select(Ui.FirstTextContaining)
                    .Where(text => text is not null)
                    .Select(text => Trim(text!))
                    .ToList();

                var leak = groups.FirstOrDefault(text => text.Contains('`', StringComparison.Ordinal));
                var note = leak is null ? string.Empty : $" — but one heading shows source markup to the operator: \"{leak}\"";

                return Act.Verified($"{string.Join(" | ", groups)}{note}");
            });

        journal.Step(
            "raise-invoice", "The completion's Draft invoice request lands under New (raised on completion, or by hand)", "mouse",
            "A Draft invoice request exists under New — completing a deliverable on a project with a pinned rate card raises it by itself (PHYSICAL_REVIEW §7c D10); without one, Raise invoice does it by hand",
            () =>
            {
                // With the rate card pinned since add-rate-card, the completion
                // step already raised the request best-effort (§7c D10), so the
                // row offers no Raise invoice action and New already reads 1.
                var alreadyNew = Ui.FirstTextContaining("New (") ?? "(no New group)";
                if (alreadyNew.Contains("New (1)", StringComparison.Ordinal) && Ui.ByName("Raise invoice for Detailed calculation pack") is null)
                    return Act.Verified($"raised on completion — \"{alreadyNew}\"; no Raise invoice action left on the row");

                if (!Act.Click("Raise invoice for Detailed calculation pack", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Confirm("Raise an invoice request"))
                    return Act.Failed(Act.LastProblem);

                Thread.Sleep(2_000);
                var newGroup = Ui.FirstTextContaining("New (") ?? "(no New group)";
                var refusal = Ui.VisibleText().FirstOrDefault(text => text.Contains("rate-card pin", StringComparison.OrdinalIgnoreCase));

                if (refusal is not null)
                    return Act.Failed($"DEFECT: refused — \"{Trim(refusal)}\" (and the refusal names the project by id, not by name)");

                return newGroup.Contains("New (1)", StringComparison.Ordinal)
                    ? Act.Verified($"\"{newGroup}\"")
                    : Act.Unknown($"\"{newGroup}\"; status \"{Act.Status()}\"");
            });

        journal.Step(
            "timesheet-grade-from-card", "Business → Timesheets → Record", "mouse",
            "The form leads with the project and offers the grade priced on its pinned rate card (until 2026-09-16 no card could exist, and the form said so in place)",
            () =>
            {
                if (!Act.ClickRow("Timesheets", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Record") is not null, 15_000))
                    return Act.Failed("the Timesheets week never rendered");

                var week = Ui.FirstTextContaining("Available:") ?? "(no utilisation line)";

                if (!Act.Click("Record", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Project") is not null && Ui.ByName("Grade") is not null, 12_000))
                    return Act.Failed("the Record prompt never appeared");

                var project = Ui.ByName("Project")?.Text ?? "(none)";
                var grade = Ui.ByName("Grade");
                var gradeText = grade?.Text ?? "(none)";
                var reason = Ui.FirstTextContaining("No rate card is pinned");

                if (!Act.Click("Cancel", settleMs: 1_200))
                    return Act.Failed(Act.LastProblem);

                return grade is { Enabled: true } && gradeText.Contains("Engineer", StringComparison.Ordinal) && reason is null
                    ? Act.Verified($"week reads \"{week}\"; Project pre-selects '{project}'; Grade is enabled and reads '{gradeText}' from the pinned card")
                    : Act.Failed($"Project '{project}'; Grade enabled={grade?.Enabled} text '{gradeText}'; reason \"{reason ?? "(none)"}\"");
            });

        journal.Step(
            "business-dashboard", "Business → Dashboard & Reports", "mouse",
            "The finance tiles read \"unavailable\" rather than a lying zero before any accounts reading",
            () =>
            {
                if (!Act.ClickRow("Dashboard & Reports", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Business dashboard") is not null, 20_000))
                    return Act.Failed("the Business dashboard never rendered");

                var unavailable = Ui.VisibleText().Count(text => text.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
                var quotes = Ui.VisibleText().FirstOrDefault(text => text.Contains("Quote", StringComparison.OrdinalIgnoreCase) && text.Contains('£', StringComparison.Ordinal));

                return unavailable > 0
                    ? Act.Verified($"{unavailable} panel(s)/tile(s) read \"unavailable\"; quotes panel reads \"{Trim(quotes ?? "(none)")}\"")
                    : Act.Unknown("nothing on the dashboard reads \"unavailable\"");
            },
            required: false);

        journal.Step(
            "home-after-work", "Home, after the work above", "mouse",
            "The cross-project cockpit now reflects the real quotation value rather than its empty state",
            () =>
            {
                if (!Act.Click("Home", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Home dashboard") is not null, 15_000))
                    return Act.Failed("Home never rendered");

                var commercial = Ui.FirstTextContaining("Quotes:") ?? "(no commercial snapshot)";
                var status = Ui.FirstTextContaining("open project(s) in total") ?? "(no project status)";
                var changed = Ui.FirstTextContaining("Recently changed") is not null;

                return Act.Verified($"\"{Trim(commercial)}\"; \"{Trim(status)}\"; Recently changed present={changed}");
            });
    }

    // ==================================================================
    // Representative surfaces
    // ==================================================================

    private static void Representative(Journal journal)
    {
        journal.Step(
            "command-palette", "Ctrl+K, then type a query", "keyboard only",
            "The palette opens over the workspace and lists a command that does not apply with its own reason, disabled",
            () =>
            {
                OsInput.Key("ctrl+k");
                if (!Ui.WaitUntil(() => Ui.ByName("Command palette query") is not null, 10_000))
                    return Act.Failed("Ctrl+K opened no palette");

                OsInput.Type("rate");
                Thread.Sleep(1_500);

                var offered = Ui.Snapshot()
                    .Where(node => node.Text.Contains("Rate Card", StringComparison.OrdinalIgnoreCase) && node.TypeName == "ListBoxItem")
                    .ToList();

                var disabled = offered.FirstOrDefault(node => !node.Enabled);
                OsInput.Key("Escape");
                Thread.Sleep(600);

                return offered.Count > 0
                    ? Act.Verified($"{offered.Count} command(s) offered for \"rate\"; e.g. \"{Trim(offered[0].Text)}\" (disabled={disabled is not null})")
                    : Act.Unknown("the palette listed no command for \"rate\"");
            });

        journal.Step(
            "header-search", "Type into the header's own search field and press Enter", "keyboard only",
            "The Command Palette opens with the query already seeded",
            () =>
            {
                if (!Act.Click("Search or run a command", settleMs: 800))
                    return Act.Failed(Act.LastProblem);

                OsInput.Type("Apollo");
                Thread.Sleep(500);
                OsInput.Key("Return");

                if (!Ui.WaitUntil(() => Ui.ByName("Command palette query") is not null, 10_000))
                    return Act.Unknown("pressing Enter in the header search opened no palette");

                var seeded = Ui.ByName("Command palette query")?.Text ?? string.Empty;
                var objects = Ui.VisibleText().FirstOrDefault(text => text.Contains(ProjectName, StringComparison.Ordinal));
                OsInput.Key("Escape");
                Thread.Sleep(600);

                return seeded.Contains("Apollo", StringComparison.OrdinalIgnoreCase)
                    ? Act.Verified($"the palette opened seeded with '{seeded}'; objects section reads \"{Trim(objects ?? "(nothing)")}\"")
                    : Act.Unknown($"the palette query reads '{seeded}'");
            },
            required: false);

        journal.Step(
            "tasks", "Rail → Tasks", "mouse",
            "The seven task buckets, each with its own honest empty text",
            () =>
            {
                if (!Act.Click("Tasks", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                var buckets = new[] { "Overdue", "Due today", "Due this week", "Later", "Reviews", "Approvals", "Finance" };
                var found = buckets.Where(bucket => Ui.VisibleText().Any(text => text.StartsWith(bucket, StringComparison.Ordinal))).ToList();

                return found.Count == buckets.Length
                    ? Act.Verified($"all seven buckets present: {string.Join(", ", found)}")
                    : Act.Unknown($"buckets found: {string.Join(", ", found)}");
            },
            required: false);

        journal.Step(
            "reference-lookup", "Engineering → Reference data → open the released material", "mouse",
            "The record's own identity, revision history and source citation are shown",
            () =>
            {
                if (!Act.Click("Engineering", settleMs: 900) || !Act.ClickRow("Reference data", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Open mat-s355j2", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                var identity = Ui.FirstTextContaining("Materials — mat-s355j2") ?? "(no identity)";
                var revision = Ui.FirstTextContaining("Released") ?? "(no state)";
                return Act.Verified($"\"{Trim(identity)}\"; \"{Trim(revision)}\"");
            },
            required: false);

        journal.Step(
            "settings", "The header's own account button → Settings", "mouse",
            "Settings opens as an area of its own, showing where the data lives and who is signed in",
            () =>
            {
                if (!Act.Click("Account — opens Settings", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByName("Settings") is not null, 15_000))
                    return Act.Unknown($"Settings did not open; Location reads '{Ui.ByName("Location")?.Text}'");

                var root = Ui.VisibleText().FirstOrDefault(text => text.Contains("realshell", StringComparison.OrdinalIgnoreCase) || text.Contains("persistence", StringComparison.OrdinalIgnoreCase));
                var principal = Ui.VisibleText().FirstOrDefault(text => text.Contains("Engineer", StringComparison.Ordinal));
                var connector = Ui.FirstTextContaining("Fake");

                return Act.Verified($"Settings opened; persistence \"{Trim(root ?? "(not shown)")}\"; principal \"{Trim(principal ?? "(not shown)")}\"; invoicing connector \"{connector ?? "(not shown)"}\"");
            },
            required: false);

        journal.Step(
            "project-close", "Open the project and close it from its own workspace", "mouse",
            "The project moves out of Open, and the workspace says it is closed",
            () =>
            {
                if (!Act.Click("Projects", settleMs: 1_500) || !Act.ClickRow("Open", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                if (!Act.ClickRow($"P-0001 {ProjectName}", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Act.Click("Close Project", settleMs: 2_000))
                    return Act.Unknown($"the project workspace offers no Close Project ({Act.LastProblem})");

                if (Ui.ByName("Continue") is not null)
                    Act.Click("Continue", settleMs: 2_500);

                Thread.Sleep(1_500);
                var project = Ui.ByName("Current project")?.Text ?? "(none)";
                var closedGroup = Ui.FirstTextContaining("Closed (under 90 days)") ?? "(no Closed group)";

                return Act.Verified($"after closing, the status bar's own project segment reads '{project}'; the tree reads \"{closedGroup}\"");
            },
            required: false);
    }

    // ==================================================================
    // Closing the application through real input
    // ==================================================================

    private static void CloseThroughRealInput(Journal journal)
    {
        journal.Step(
            "close", "Close the application from the window itself", "X11 WM_DELETE_WINDOW (xdotool windowclose)",
            "The window closes cleanly, with no crash log and no error dialog",
            () =>
            {
                OsInput.CloseWindow();

                var closed = !Ui.WaitUntil(() => !OsInput.WindowExists("TempestOS"), 20_000)
                    ? OsInput.WindowExists("TempestOS")
                    : false;

                return closed
                    ? Act.Unknown("the window was still on the X server 20 s after the close message")
                    : Act.Verified("the window closed on the operating system's own close message");
            });
    }

    // ==================================================================

    private static string WriteSampleEvidenceFile(string outputDirectory)
    {
        // A real 1x1 PNG, written by the runner so the file dialog has a
        // genuine file of a genuine type to pick.
        var path = Path.Combine(outputDirectory, "evidence-sample.png");
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        return path;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Trim(string value) => value.Length <= 200 ? value : value[..200] + "…";
}
