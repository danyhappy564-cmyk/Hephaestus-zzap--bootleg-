using System.Globalization;

namespace Hephaestus;

/// <summary>
/// Korean / English for the server side.
/// - Server log lines follow the language of the PC running the server (Windows display language).
/// - Trader texts (name, description, location) are written per game locale, so the game shows
///   whatever language the player picked in the game settings ("kr" = Korean, every other = English).
/// </summary>
public static class Loc
{
    /// <summary>True when the server PC's display language is Korean.</summary>
    public static bool Korean { get; set; } = DetectKorean();

    public static string L(string korean, string english) => Korean ? korean : english;

    private static bool DetectKorean()
    {
        try
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"
                   || CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Trader texts for one game locale code ("kr", "en", "ru", ...).</summary>
    public static TraderTexts TraderTextsFor(string localeCode) =>
        localeCode == "kr" ? TraderTextsKorean : TraderTextsEnglish;

    public static readonly TraderTexts TraderTextsEnglish = new(
        FullName: "Hephaestus",
        FirstName: "Hephaestus",
        Nickname: "Hephaestus",
        Location: "Greece",
        Description: "You share the reseller license of your creations to Hephaestus and in return you get a hefty discount.");

    public static readonly TraderTexts TraderTextsKorean = new(
        FullName: "헤파이스토스",
        FirstName: "헤파이스토스",
        Nickname: "헤파이스토스",
        Location: "그리스",
        Description: "당신이 직접 만든 무기 설계(프리셋)의 재판매 권리를 헤파이스토스에게 넘기는 대신, 그 총을 크게 할인된 값에 살 수 있습니다.");
}

public sealed record TraderTexts(string FullName, string FirstName, string Nickname, string Location, string Description);
