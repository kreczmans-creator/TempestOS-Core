using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Shot-fix integration, third re-shoot: the scalar text input 'element' of bs.glaser showed the placeholder "text", which says nothing its
/// label does not. A text input has no placeholder, as table cells have none; a number input keeps "number".
/// </summary>
public sealed class ScalarTextPlaceholderTests
{
    private static CalcSheetView Open(string templateId)
    {
        var all = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(CalcFeatures.All));
        var view = new CalcSheetView();
        view.ShowCatalogue(all.Current());
        view.SelectTemplate(templateId);
        return view;
    }

    private static TextBox Field(CalcSheetView view, string symbol) =>
        view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == symbol);

    [AvaloniaFact]
    public void The_Glaser_Element_Text_Field_Has_No_Placeholder()
    {
        var view = Open("bs.glaser");
        var element = Field(view, "element");
        Assert.True(string.IsNullOrEmpty(element.Text));
        Assert.Null(element.Watermark);
    }

    [AvaloniaFact]
    public void No_Scalar_Text_Input_Of_Any_Template_Shows_A_Placeholder_And_Number_Inputs_Keep_Theirs()
    {
        var all = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(CalcFeatures.All));
        var texts = 0;

        foreach (var entry in all.Current())
        {
            var view = Open(entry.Template.Id);

            foreach (var spec in entry.Template.Inputs.Where(s => s.Kind == CalcInputKind.Text && s.Choices is not { Count: > 0 }))
            {
                if (view.FieldRow(spec.Symbol) is null)
                    continue;

                Assert.True(Field(view, spec.Symbol).Watermark is null, $"{entry.Template.Id} {spec.Symbol} shows a placeholder");
                texts++;
            }
        }

        Assert.True(texts > 0, "no scalar text input was found to check");
        Assert.Equal("number", Field(Open("bs.glaser"), "T_int").Watermark);
    }
}
