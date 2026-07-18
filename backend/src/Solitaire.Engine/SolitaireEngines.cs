using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// Registry of the available <see cref="ISolitaireEngine"/> implementations,
/// keyed by their <see cref="ISolitaireEngine.Variant"/> id. Lets the API resolve
/// an engine from a submitted game's variant and verify it uniformly.
/// </summary>
public static class SolitaireEngines
{
    private static readonly ImmutableDictionary<string, ISolitaireEngine> Registry =
        new ISolitaireEngine[]
        {
            new KlondikeEngine(), new SpiderEngine(), new FreeCellEngine(), new PyramidEngine(), new TriPeaksEngine(),
        }
            .ToImmutableDictionary(e => e.Variant, StringComparer.OrdinalIgnoreCase);

    /// <summary>All supported variant ids.</summary>
    public static IReadOnlyCollection<string> Variants { get; } = [.. Registry.Keys];

    /// <summary>Gets the engine for a variant id, or throws if it is unknown.</summary>
    public static ISolitaireEngine For(string variant) =>
        Registry.TryGetValue(variant, out var engine)
            ? engine
            : throw new ArgumentException($"Unknown solitaire variant '{variant}'.", nameof(variant));

    /// <summary>Tries to get the engine for a variant id.</summary>
    public static bool TryGet(string variant, out ISolitaireEngine engine) =>
        Registry.TryGetValue(variant, out engine!);
}
