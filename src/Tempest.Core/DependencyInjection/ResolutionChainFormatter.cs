namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Formats a service construction chain into a human-readable "A -> B -> C" string,
/// shared by every <see cref="ServiceResolutionException"/> subtype so the format
/// stays consistent without duplicating the formatting logic in each one.
/// </summary>
internal static class ResolutionChainFormatter
{
    /// <summary>
    /// Formats <paramref name="resolutionChain"/>, optionally appending <paramref name="finalType"/>.
    /// </summary>
    public static string Format(IReadOnlyList<Type> resolutionChain, Type? finalType = null)
    {
        var names = resolutionChain.Select(type => type.Name);

        if (finalType is not null)
            names = names.Append(finalType.Name);

        return string.Join(" -> ", names);
    }

    /// <summary>
    /// Gets the originally requested top-level service type: the first entry in
    /// <paramref name="resolutionChain"/>, or <paramref name="fallback"/> if the chain
    /// is empty (meaning the failure occurred on the very first, top-level resolution).
    /// </summary>
    public static Type RequestedService(IReadOnlyList<Type> resolutionChain, Type fallback) =>
        resolutionChain.Count > 0 ? resolutionChain[0] : fallback;
}
