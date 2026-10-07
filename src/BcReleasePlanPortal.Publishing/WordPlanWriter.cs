using System.Globalization;
using BcReleasePlanPortal.Domain;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BcReleasePlanPortal.Publishing;

/// <summary>
/// Renders a <see cref="PlanDocument"/> as a Word file: A4, Calibri, a calm blue palette, and the
/// design doc's sections (§9.1) in English. Stands in for the company letterhead template until it
/// is available — everything visual is in the constants and <see cref="AddStyles"/> below.
/// The document has to stand alone for a customer who never opens the portal.
/// </summary>
public static class WordPlanWriter
{
    private const string Navy = "1F3A5F";      // title and section headings
    private const string Blue = "2E6DA4";      // item headings, accents
    private const string PaleBlue = "EAF1F8";  // table headers, customer notes
    private const string Rule = "C9D8E8";      // table and banner borders
    private const string Ink = "1F2933";       // body text
    private const string Muted = "5B6B7B";     // meta lines
    private const string WarnFill = "FFF4E0";  // preview / sample banners
    private const string WarnInk = "7A3E06";

    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public static byte[] Write(PlanDocument plan)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            AddStyles(main);
            var footerId = AddFooter(main, plan);

            var body = new Body();
            WriteTitleBlock(body, plan);
            WriteSummary(body, plan);
            WriteSection(body, plan, PlanSection.Mandatory, "Mandatory changes",
                "Changes Microsoft is making that need action, whatever else is decided.");
            WriteSection(body, plan, PlanSection.Recommended, "Recommended new functionality",
                "New and improved features we recommend adopting or testing.");
            WriteSection(body, plan, PlanSection.ForInformation, "For information",
                "Relevant changes that are waiting on something before they can go ahead.");
            WriteNextSteps(body, plan);
            WriteChanges(body, plan);

            body.Append(new SectionProperties(
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1247, Right = 1247, Header = 567, Footer = 567, Gutter = 0 }));

            main.Document = new Document(body);
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static void WriteTitleBlock(Body body, PlanDocument plan)
    {
        body.Append(Para("Release plan", "Title"));
        body.Append(Para(plan.CustomerName, "Subtitle"));
        body.Append(Para(
            $"Microsoft Dynamics 365 Business Central  ·  {plan.PeriodLabel}  ·  Version {plan.Version}  ·  {plan.Date.ToString("d MMMM yyyy", English)}",
            "Meta"));

        if (plan.IsSample)
        {
            body.Append(Banner("SAMPLE CUSTOMER — NOT FOR DISTRIBUTION. This customer profile is made up for testing the portal."));
        }

        if (plan.IsPreview)
        {
            body.Append(Banner("PREVIEW — not published. The content can still change; a published version is frozen and numbered."));
        }
    }

    private static void WriteSummary(Body body, PlanDocument plan)
    {
        body.Append(Para("Summary", "Heading1"));

        var mandatory = plan.Lines.Count(l => l.Section == PlanSection.Mandatory);
        var recommended = plan.Lines.Count(l => l.Section == PlanSection.Recommended);
        var info = plan.Lines.Count(l => l.Section == PlanSection.ForInformation);

        if (plan.Lines.Count == 0)
        {
            body.Append(Para($"No Business Central changes in {plan.PeriodLabel} need attention for {plan.CustomerName} yet."));
            return;
        }

        body.Append(Para(
            $"This plan covers {Count(plan.Lines.Count, "Business Central change")} that affect {plan.CustomerName} in {plan.PeriodLabel}: " +
            $"{mandatory} mandatory, {recommended} recommended and {info} for information."));

        var firstMandatory = plan.Lines.FirstOrDefault(l => l.Section == PlanSection.Mandatory);
        if (firstMandatory is not null)
        {
            body.Append(Para($"Most urgent: {firstMandatory.Title} ({When(firstMandatory)})."));
        }

        var table = NewTable(["Change", "Section", "Version", "Expected", "Decision"], [40, 18, 10, 14, 18]);
        foreach (var line in plan.Lines)
        {
            table.Append(Row([line.Title, SectionTitle(line.Section), line.TargetVersion ?? "—", Month(line.GaDate), DecisionLabel(line.Decision)]));
        }

        body.Append(table);
    }

    private static void WriteSection(Body body, PlanDocument plan, PlanSection section, string title, string intro)
    {
        var lines = plan.Lines.Where(l => l.Section == section).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        body.Append(Para(title, "Heading1"));
        body.Append(Para(intro, "Meta"));

        foreach (var line in lines)
        {
            body.Append(Para(line.Title, "Heading2"));
            body.Append(Para(
                string.Join("  ·  ", new[]
                {
                    Spaced(line.ChangeType.ToString()),
                    line.TargetVersion is null ? null : $"Version {line.TargetVersion}",
                    line.GaDate is null ? null : $"Expected {Month(line.GaDate)}",
                    line.EffortBand == EffortBand.None ? null : $"Effort {line.EffortBand}",
                    $"Risk {line.Risk}",
                    $"Decision: {DecisionLabel(line.Decision)}",
                }.Where(s => s is not null)),
                "Meta"));

            if (!line.HasReviewedNote)
            {
                body.Append(Banner("No reviewed impact note yet — write and review one before publishing."));
            }

            if (line.Summary.Length > 0)
            {
                body.Append(Para(line.Summary));
            }

            LabelledPara(body, "Why it matters", line.WhyItMatters);
            LabelledPara(body, "What to do", line.Action);

            if (line.CustomerNote.Length > 0)
            {
                body.Append(Note($"For {plan.CustomerName}: ", line.CustomerNote));
            }
        }
    }

    private static void WriteNextSteps(Body body, PlanDocument plan)
    {
        var actions = plan.Lines.Where(l => l.Action.Length > 0).ToList();
        body.Append(Para("Next steps", "Heading1"));
        if (actions.Count == 0)
        {
            body.Append(Para("No actions are required yet."));
            return;
        }

        var table = NewTable(["What to do", "Change", "When", "Decision"], [44, 30, 12, 14]);
        foreach (var line in actions)
        {
            table.Append(Row([line.Action, line.Title, When(line), DecisionLabel(line.Decision)]));
        }

        body.Append(table);
    }

    private static void WriteChanges(Body body, PlanDocument plan)
    {
        body.Append(Para("Changes since previous version", "Heading1"));
        if (plan.Changes is null)
        {
            body.Append(Para("This is the first version of this plan."));
            return;
        }

        var c = plan.Changes;
        if (c.Added.Count == 0 && c.Removed.Count == 0 && c.DecisionChanged.Count == 0)
        {
            body.Append(Para($"No changes to the list since version {c.PreviousVersion}; descriptions may have been updated."));
            return;
        }

        body.Append(Para($"Compared with version {c.PreviousVersion}:"));
        foreach (var title in c.Added) body.Append(Para($"Added: {title}", "ListItem"));
        foreach (var title in c.Removed) body.Append(Para($"Removed: {title}", "ListItem"));
        foreach (var (title, from, to) in c.DecisionChanged) body.Append(Para($"Decision changed from {DecisionLabel(from)} to {DecisionLabel(to)}: {title}", "ListItem"));
    }

    // ---- Building blocks -------------------------------------------------------------------

    private static Paragraph Para(string text, string? style = null)
    {
        var p = new Paragraph();
        if (style is not null) p.Append(new ParagraphProperties(new ParagraphStyleId { Val = style }));
        p.Append(TextRun(text));
        return p;
    }

    private static void LabelledPara(Body body, string label, string text)
    {
        if (text.Length == 0) return;
        body.Append(new Paragraph(
            new Run(new RunProperties(new Bold(), new Color { Val = Navy }), new Text(label + ": ") { Space = SpaceProcessingModeValues.Preserve }),
            TextRun(text)));
    }

    private static Paragraph Note(string label, string text) => new(
        new ParagraphProperties(
            new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 18, Color = Blue, Space = 6 }),
            new Shading { Val = ShadingPatternValues.Clear, Fill = PaleBlue },
            new Indentation { Left = "113" }),
        new Run(new RunProperties(new Bold(), new Color { Val = Navy }), new Text(label) { Space = SpaceProcessingModeValues.Preserve }),
        TextRun(text));

    private static Paragraph Banner(string text) => new(
        new ParagraphProperties(
            new ParagraphBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "E6A23C", Space = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "E6A23C", Space = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "E6A23C", Space = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "E6A23C", Space = 4 }),
            new Shading { Val = ShadingPatternValues.Clear, Fill = WarnFill },
            new SpacingBetweenLines { Before = "120", After = "120" }),
        new Run(new RunProperties(new Bold(), new Color { Val = WarnInk }, new FontSize { Val = "19" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    /// <summary>Text with line breaks kept (impact notes may span lines).</summary>
    private static Run TextRun(string text)
    {
        var run = new Run();
        var parts = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            run.Append(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        return run;
    }

    private static Table NewTable(string[] headers, int[] widthPercents)
    {
        var table = new Table(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = Rule },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Rule },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Rule }),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 100, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 100, Type = TableWidthValues.Dxa })));

        var grid = new TableGrid();
        foreach (var pct in widthPercents) grid.Append(new GridColumn { Width = (9412 * pct / 100).ToString(English) });
        table.Append(grid);

        var header = new TableRow(new TableRowProperties(new TableHeader()));
        foreach (var h in headers)
        {
            header.Append(new TableCell(
                new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = PaleBlue }),
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "TableText" }),
                    new Run(new RunProperties(new Bold(), new Color { Val = Navy }), new Text(h)))));
        }

        table.Append(header);
        return table;
    }

    private static TableRow Row(string[] cells)
    {
        var row = new TableRow(new TableRowProperties(new CantSplit()));
        foreach (var c in cells)
        {
            row.Append(new TableCell(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "TableText" }), TextRun(c))));
        }

        return row;
    }

    private static string AddFooter(MainDocumentPart main, PlanDocument plan)
    {
        var part = main.AddNewPart<FooterPart>();
        part.Footer = new Footer(new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Meta" },
                new Tabs(new TabStop { Val = TabStopValues.Right, Position = 9412 })),
            new Run(new Text($"Release plan · {plan.CustomerName} · version {plan.Version}{(plan.IsPreview ? " (preview)" : "")}") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new TabChar(), new Text("Page ") { Space = SpaceProcessingModeValues.Preserve }),
            new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " },
            new Run(new Text(" of ") { Space = SpaceProcessingModeValues.Preserve }),
            new SimpleField(new Run(new Text("1"))) { Instruction = " NUMPAGES " }));
        part.Footer.Save();
        return main.GetIdOfPart(part);
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri", EastAsia = "Calibri" },
                    new Color { Val = Ink },
                    new FontSize { Val = "21" },
                    new Languages { Val = "en-GB" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto }))),
            Style("Normal", "Normal", null, null, null, isDefault: true),
            Style("Title", "Title", new StyleRunProperties(new Bold(), new Color { Val = Navy }, new FontSize { Val = "48" }),
                new SpacingBetweenLines { After = "0" }),
            Style("Subtitle", "Subtitle", new StyleRunProperties(new Color { Val = Blue }, new FontSize { Val = "32" }),
                new SpacingBetweenLines { After = "60" }),
            Style("Meta", "Meta", new StyleRunProperties(new Color { Val = Muted }, new FontSize { Val = "18" }), null),
            Style("Heading1", "heading 1", new StyleRunProperties(new Bold(), new Color { Val = Navy }, new FontSize { Val = "30" }),
                new SpacingBetweenLines { Before = "360", After = "120" }, outline: 0, keepNext: true, bottomRule: true),
            Style("Heading2", "heading 2", new StyleRunProperties(new Bold(), new Color { Val = Blue }, new FontSize { Val = "24" }),
                new SpacingBetweenLines { Before = "240", After = "40" }, outline: 1, keepNext: true),
            Style("ListItem", "List item", null, new SpacingBetweenLines { After = "60" }, indentLeft: "284"),
            Style("TableText", "Table text", new StyleRunProperties(new FontSize { Val = "19" }), new SpacingBetweenLines { After = "0" }));
        part.Styles.Save();
    }

    private static Style Style(string id, string name, StyleRunProperties? run, SpacingBetweenLines? spacing,
        string? indentLeft = null, bool isDefault = false, int? outline = null, bool keepNext = false, bool bottomRule = false)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = id, Default = isDefault ? true : null };
        style.Append(new StyleName { Val = name });
        if (!isDefault) style.Append(new BasedOn { Val = "Normal" });
        style.Append(new PrimaryStyle());

        var p = new StyleParagraphProperties();
        if (keepNext) p.Append(new KeepNext());
        if (bottomRule) p.Append(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = Rule, Space = 2 }));
        if (spacing is not null) p.Append(spacing);
        if (indentLeft is not null) p.Append(new Indentation { Left = indentLeft });
        if (outline is not null) p.Append(new OutlineLevel { Val = outline });
        if (p.HasChildren) style.Append(p);
        if (run is not null) style.Append(run);
        return style;
    }

    // ---- Wording ---------------------------------------------------------------------------

    public static string SectionTitle(PlanSection s) => s switch
    {
        PlanSection.Mandatory => "Mandatory",
        PlanSection.Recommended => "Recommended",
        _ => "For information",
    };

    public static string DecisionLabel(CustomerItemDecision d) => d switch
    {
        CustomerItemDecision.Undecided => "To be agreed",
        CustomerItemDecision.TestFirst => "Test first",
        _ => d.ToString(),
    };

    private static string When(PlanLine l) =>
        l.TargetVersion is not null ? $"version {l.TargetVersion}" : l.GaDate is not null ? Month(l.GaDate) : "date not announced";

    private static string Month(DateOnly? d) => d?.ToString("MMMM yyyy", English) ?? "—";

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    private static string Spaced(string pascal) =>
        string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
}
