using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project Workspace's own Documents area — this project's documents
/// and drawings, and the files held against them.
/// </summary>
/// <remarks>
/// <para>
/// The area this view replaced was declared <c>Implemented</c> and drew a
/// <see cref="DeclaredCapabilityView"/>: a glyph, a title, a paragraph and
/// nothing else. It is a real surface now — the project's own documents,
/// resolved transitively through <see cref="ProjectMembership"/>, each
/// file openable in the `TD-80` viewer.
/// </para>
/// <para>
/// <b>Opening a document is not navigation.</b> The Open button raises
/// <see cref="OpenAttachmentRequested"/> and this view does nothing else —
/// the shell decides where a document opens, exactly as the object editor
/// already does. The project stays open, the module stays where it was,
/// and the viewer arrives as an ordinary `TD-72` panel beside the work
/// rather than on top of it. Because that panel lives in the Engineering
/// workspace's layout, the row says where the document went
/// (<see cref="MarkOpened"/>): a button that appears to do nothing is
/// indistinguishable from a broken one.
/// </para>
/// <para>
/// Nothing here reads attachment <em>content</em>. Listing a project's
/// hundred drawings must not load a hundred files; the bytes are `TD-31`'s
/// and are read when a document is actually opened.
/// </para>
/// </remarks>
public sealed class ProjectDocumentsView : UserControl
{
    /// <summary>The heading shown above the register.</summary>
    public const string Heading = "Documents & Drawings";

    /// <summary>What the surface says when the project holds no documents at all.</summary>
    public const string EmptyHeadline = "No documents in this project";

    /// <summary>What the surface says against a document that has no file attached.</summary>
    public const string NoFilesNote = "No file attached";

    /// <summary>What the surface says against a file it has opened in the viewer.</summary>
    public const string OpenedNote = "Open in the Engineering workspace";

    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly Dictionary<Guid, TextBlock> _openedNotes = [];
    private readonly HashSet<Guid> _opened = [];
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    /// <summary>Raised when the user asks to open one of this project's files.</summary>
    /// <remarks>Carries the owning object and the attachment, which is exactly what the `TD-80` launcher needs.</remarks>
    public event Action<Guid, Guid>? OpenAttachmentRequested;

    /// <summary>Initialises a new instance of the <see cref="ProjectDocumentsView"/> class.</summary>
    public ProjectDocumentsView()
    {
        var heading = new TextBlock
        {
            Text = Heading,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(heading);
        root.Children.Add(_summary);
        root.Children.Add(_list);

        AutomationProperties.SetName(this, Heading);
        Content = new ScrollViewer { Content = root };
    }

    /// <summary>The entries currently on screen, in the order they are shown.</summary>
    public IReadOnlyList<ProjectDocumentEntry> Entries { get; private set; } = [];

    /// <summary>Whether the surface is showing its empty state.</summary>
    public bool IsShowingEmptyState { get; private set; } = true;

    /// <summary>The summary line, exactly as a user reads it.</summary>
    public string SummaryText => _summary.Text ?? string.Empty;

    /// <summary>The attachments this surface has opened in the viewer.</summary>
    public IReadOnlyCollection<Guid> OpenedAttachmentIds => _opened;

    /// <summary>
    /// Records that <paramref name="attachmentId"/> is now open in the
    /// viewer, and says so on its row.
    /// </summary>
    /// <remarks>
    /// The viewer is a panel in the Engineering workspace's own layout, so
    /// a document opened from here does not appear on the project area the
    /// user is standing on — and a button that looks like it did nothing is
    /// indistinguishable from a broken one. The row says where the document
    /// went instead. Opening still changes no navigation state at all: the
    /// project, the module and the area are exactly as they were.
    /// </remarks>
    public void MarkOpened(Guid attachmentId)
    {
        _opened.Add(attachmentId);

        if (_openedNotes.TryGetValue(attachmentId, out var note))
            note.IsVisible = true;
    }

    /// <summary>Renders <paramref name="entries"/> for the project named <paramref name="projectLabel"/>.</summary>
    public void Show(IReadOnlyList<ProjectDocumentEntry> entries, string? projectLabel)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = entries;
        _list.Children.Clear();
        _openedNotes.Clear();

        var project = string.IsNullOrWhiteSpace(projectLabel) ? "this project" : projectLabel;
        var files = entries.Sum(e => e.Attachments.Count);

        IsShowingEmptyState = entries.Count == 0;
        if (IsShowingEmptyState)
        {
            // An empty project is a normal state, not a failure: it says
            // what is true and what to do about it, and never suggests
            // something went wrong.
            _summary.Text = $"{project} holds no documents or drawings yet.";
            _list.Children.Add(EmptyState(
                EmptyHeadline,
                "Create a Document or Drawing in the Engineering Workspace and attach a file to it; it will appear here, with everything else this project owns."));
            return;
        }

        _summary.Text = files == 1
            ? $"{entries.Count} document(s) in {project}, holding 1 file."
            : $"{entries.Count} document(s) in {project}, holding {files} files.";

        foreach (var entry in entries)
            _list.Children.Add(BuildEntry(entry));
    }

    private static Control EmptyState(string headline, string detail)
    {
        var stack = new StackPanel
        {
            Spacing = DesignTokens.SpaceSm,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, DesignTokens.SpaceXxl, 0, 0),
            MaxWidth = 460,
        };

        stack.Children.Add(new TextBlock
        {
            Text = headline,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        stack.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.8,
        });

        return stack;
    }

    private Control BuildEntry(ProjectDocumentEntry entry)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var title = string.IsNullOrWhiteSpace(entry.Identifier)
            ? entry.DisplayName
            : $"{entry.Identifier} — {entry.DisplayName}";

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        header.Children.Add(new TextBlock { Text = title, FontWeight = DesignTokens.WeightHeading, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock
        {
            Text = entry.Kind,
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        });
        rows.Children.Add(header);

        if (!entry.HasFiles)
        {
            // A document with no file is still a document. Saying so is a
            // different statement from an empty project, and the user
            // needs to be able to tell the two apart.
            rows.Children.Add(new TextBlock
            {
                Text = NoFilesNote,
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.7,
                Margin = new Thickness(DesignTokens.SpaceLg, 0, 0, 0),
            });
        }

        foreach (var attachment in entry.Attachments)
            rows.Children.Add(BuildAttachmentRow(entry, attachment));

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
        };

        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Control BuildAttachmentRow(ProjectDocumentEntry entry, ProjectDocumentAttachment attachment)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceMd,
            Margin = new Thickness(DesignTokens.SpaceLg, 0, 0, 0),
        };

        row.Children.Add(new TextBlock
        {
            Text = $"📎 {attachment.FileName}",
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{attachment.ContentType}, {attachment.SizeInBytes:N0} bytes",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var open = new Button
        {
            Content = "Open",
            Padding = new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceXs),
            FontSize = DesignTokens.FontSizeBody,
        };

        AutomationProperties.SetName(open, $"Open {attachment.FileName}");

        // Offered for every attachment rather than only those with stored
        // content: "this file is missing" is something the viewer says
        // clearly, and a greyed-out Open would leave the user guessing
        // whether the file is gone, the format is unsupported, or the
        // application is broken. The identical reasoning as the object
        // editor's own Open button.
        open.Click += (_, _) => OpenAttachmentRequested?.Invoke(entry.ObjectId, attachment.Id);
        row.Children.Add(open);

        var opened = new TextBlock
        {
            Text = OpenedNote,
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = _opened.Contains(attachment.Id),
        };

        _openedNotes[attachment.Id] = opened;
        row.Children.Add(opened);

        return row;
    }
}
