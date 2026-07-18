namespace Solitaire.Engine;

/// <summary>
/// Pyramid has no player-configurable rules (unlimited redeals is a fixed rule,
/// not a knob). This type exists only for parity with the other variants'
/// <c>Options</c> types and their <c>OptionsFromBag</c> contract.
/// </summary>
public readonly record struct PyramidOptions;
