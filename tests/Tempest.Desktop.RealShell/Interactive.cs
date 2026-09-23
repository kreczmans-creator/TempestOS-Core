using System.Globalization;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// `--mode drive`: a plain command loop over stdin for exploring the real,
/// running application - dump the tree, click a caption, type, press a key,
/// take a screenshot. It exists because the journey's own steps were
/// discovered by driving the real product rather than by reading its source,
/// and it keeps that discovery reproducible. It is not part of the journey
/// and asserts nothing.
/// </summary>
internal static class Interactive
{
    internal static void Run()
    {
        Console.WriteLine("ready");

        while (Console.ReadLine() is { } line)
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var command = space < 0 ? line.Trim() : line[..space].Trim();
            var rest = space < 0 ? string.Empty : line[(space + 1)..].Trim();

            switch (command)
            {
                case "":
                    break;

                case "quit":
                    return;

                case "tree":
                    foreach (var node in Ui.Snapshot().Where(node => node.HasArea))
                        Console.WriteLine(node);
                    break;

                case "names":
                    foreach (var node in Ui.Snapshot().Where(node => node.HasArea && node.Name.Length > 0))
                        Console.WriteLine(node);
                    break;

                case "text":
                    foreach (var node in Ui.Snapshot().Where(node => node.HasArea && node.Text.Length > 0))
                        Console.WriteLine(node);
                    break;

                case "find":
                    foreach (var node in Ui.Snapshot().Where(node =>
                                 node.Name.Contains(rest, StringComparison.OrdinalIgnoreCase)
                                 || node.Text.Contains(rest, StringComparison.OrdinalIgnoreCase)))
                        Console.WriteLine(node);
                    break;

                case "click":
                    Click(rest);
                    break;

                case "act":
                    Console.WriteLine(Act.Click(rest) ? "clicked" : Act.LastProblem);
                    break;

                case "row":
                    Console.WriteLine(Act.ClickRow(rest) ? "clicked row" : Act.LastProblem);
                    break;

                case "into":
                    var into = rest.Split('=', 2);
                    Console.WriteLine(Act.TypeInto(into[0].Trim(), into.Length > 1 ? into[1].Trim() : string.Empty) ? "typed" : Act.LastProblem);
                    break;

                case "choose":
                    var choose = rest.Split('=', 2);
                    Console.WriteLine(Act.Choose(choose[0].Trim(), choose.Length > 1 ? choose[1].Trim() : string.Empty) ? "chosen" : Act.LastProblem);
                    break;

                case "clickxy":
                    var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    OsInput.Click(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;

                case "type":
                    OsInput.Type(rest);
                    break;

                case "clear":
                    OsInput.ClearAndType(rest);
                    break;

                case "key":
                    OsInput.Key(rest);
                    break;

                case "shot":
                    Console.WriteLine(OsInput.Screenshot(Path.Combine("/tmp", rest.Length == 0 ? "drive.png" : rest)) ? "shot ok" : "shot failed");
                    break;

                case "sleep":
                    Thread.Sleep(int.Parse(rest, CultureInfo.InvariantCulture));
                    break;

                default:
                    Console.WriteLine($"? {command}");
                    break;
            }

            Console.WriteLine("--done--");
        }
    }

    private static void Click(string caption)
    {
        var node = Ui.Clickable(caption);
        if (node is null)
        {
            Console.WriteLine($"not found: {caption}");
            return;
        }

        Console.WriteLine($"clicking {node}");
        OsInput.Click(node.CentreX, node.CentreY);
        Thread.Sleep(700);
    }
}
