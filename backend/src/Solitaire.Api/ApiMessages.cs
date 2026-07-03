namespace Solitaire.Api;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;ApiMessages&gt;</c>. The strings live in
/// <c>Resources/ApiMessages.resx</c> (English, the default culture) and
/// <c>Resources/ApiMessages.tr.resx</c> (Turkish). The request culture is chosen
/// by the standard providers (querystring / cookie / <c>Accept-Language</c>).
/// </summary>
public sealed class ApiMessages;
