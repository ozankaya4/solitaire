namespace Solitaire.Engine;

/// <summary>
/// FreeCell has no player-configurable rules (no draw count, no redeals — the
/// deal alone determines the game). This type exists only for parity with the
/// other variants' <c>Options</c> types and their <c>OptionsFromBag</c> contract.
/// </summary>
public readonly record struct FreeCellOptions;
