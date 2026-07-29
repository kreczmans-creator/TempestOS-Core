namespace Tempest.App.Shell;

/// <summary>
/// A minimal, plain-text <see cref="IPage"/> — a title, a rule, and a
/// message. The only page shape this minimum viable Shell needs.
/// </summary>
/// <remarks>
/// Used both for the Shell's own built-in, hand-registered pages and for the
/// generic "unknown page" placeholder shown for any <c>NavigationItem.Id</c>
/// the Shell has no registration for — the same type serves both cases,
/// since neither needs anything more than a title and a message.
/// </remarks>
public sealed class PlaceholderPage : IPage
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlaceholderPage"/> class.
    /// </summary>
    /// <param name="title">The page's display title.</param>
    /// <param name="message">The page's body text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="title"/> or <paramref name="message"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public PlaceholderPage(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must not be null, empty, or whitespace.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must not be null, empty, or whitespace.", nameof(message));

        Title = title;
        Message = message;
    }

    /// <summary>
    /// Gets the page's display title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the page's body text.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    public void Render(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(Title);
        writer.WriteLine(new string('-', Title.Length));
        writer.WriteLine(Message);
    }
}
