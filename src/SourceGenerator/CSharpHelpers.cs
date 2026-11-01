using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace RazorSlices.SourceGenerator;

internal static class CSharpHelpers
{
    private static readonly ImmutableHashSet<string> reservedKeywords = SyntaxFacts.GetKeywordKinds().Select(SyntaxFacts.GetText).ToImmutableHashSet();

    private static bool IsKeyword(string value)
    {
        return reservedKeywords.Contains(value);
    }

    internal static bool IsValidNamespace(string text) => IsValidTypeName(text, allowPeriod: true);

    internal static bool IsValidTypeName(string text, bool allowPeriod = false)
    {
        // Must be 1 character or longer.

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Must start with a letter or underscore.

        if (!char.IsLetter(text[0]) && text[0] != '_')
        {
            return false;
        }

        // Each character must be
        // Letter (Lu, Ll, Lt, Lm, Lo or Nl), digit (Nd), connecting (Pc), combining (Mn or Mc), and formatting (Cf) categories.

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (allowPeriod && character == '.')
            {
                continue;
            }

            var uc = CharUnicodeInfo.GetUnicodeCategory(character);

            switch (uc)
            {
                case UnicodeCategory.UppercaseLetter:       // Lu
                case UnicodeCategory.LowercaseLetter:       // Ll
                case UnicodeCategory.TitlecaseLetter:       // Lt
                case UnicodeCategory.ModifierLetter:        // Lm
                case UnicodeCategory.OtherLetter:           // Lo
                case UnicodeCategory.LetterNumber:          // Nl
                case UnicodeCategory.DecimalDigitNumber:    // Nd
                case UnicodeCategory.ConnectorPunctuation:  // Pc
                case UnicodeCategory.NonSpacingMark:        // Mn
                case UnicodeCategory.SpacingCombiningMark:  // Mc
                case UnicodeCategory.Format:                // Cf
                    break;
                default:
                    return false;
            }
        }

        if (IsKeyword(text))
        {
            return false;
        }

        return true;
    }

    internal static string CreateValidNamespace(string text) => CreateValidTypeName(text, allowPeriod: true);

    internal static string CreateValidTypeName(string text, bool allowPeriod = false)
    {
        // Each character must be
        // Letter (Lu, Ll, Lt, Lm, Lo or Nl), digit (Nd), connecting (Pc), combining (Mn or Mc), and formatting (Cf) categories.
        // Anything outside that is automatically replaced using _

        char[] characters = text.ToCharArray();

        for (int i = 0; i < characters.Length; i++)
        {
            char character = text[i];

            if (allowPeriod && character == '.')
            {
                continue;
            }

            var uc = CharUnicodeInfo.GetUnicodeCategory(character);

            switch (uc)
            {
                case UnicodeCategory.UppercaseLetter:       // Lu
                case UnicodeCategory.LowercaseLetter:       // Ll
                case UnicodeCategory.TitlecaseLetter:       // Lt
                case UnicodeCategory.ModifierLetter:        // Lm
                case UnicodeCategory.OtherLetter:           // Lo
                case UnicodeCategory.LetterNumber:          // Nl
                case UnicodeCategory.DecimalDigitNumber:    // Nd
                case UnicodeCategory.ConnectorPunctuation:  // Pc
                case UnicodeCategory.NonSpacingMark:        // Mn
                case UnicodeCategory.SpacingCombiningMark:  // Mc
                case UnicodeCategory.Format:                // Cf
                    break;
                default:
                    characters[i] = '_';
                    break;
            }
        }

        var result = new string(characters);

        // Type name must start with a letter or underscore.

        if (!char.IsLetter(result[0]) && result[0] != '_')
        {
            result = '_' + result;
        }

        while (IsKeyword(result))
        {
            result = "_" + result;
        }

        return result;
    }
}
