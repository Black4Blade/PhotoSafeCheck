using System.Reflection;

namespace PhotoSafeCheck;

/// <summary>Read packaged media directly from the assembly (no application sidecars).</summary>
internal static class BundledAssets
{
    internal static Stream? Open(string name) =>
        typeof(BundledAssets).Assembly.GetManifestResourceStream("PhotoSafeCheck.Assets." + name);

    internal static Stream? OpenLinks() =>
        typeof(BundledAssets).Assembly.GetManifestResourceStream("PhotoSafeCheck.ExtensionLinks.json");
}
