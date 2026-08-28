using UIKit;

namespace Tempest.Companion.iOS;

/// <summary>The iOS head's process entry point — the standard Avalonia.iOS main.</summary>
public static class Program
{
    /// <summary>The process entry point.</summary>
    public static void Main(string[] args) =>
        UIApplication.Main(args, null, typeof(AppDelegate));
}
