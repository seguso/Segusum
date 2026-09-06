using System;
using System.Collections.Generic;

namespace Segusum.Scripting.Core;

/// <summary>Shared literal decoding and translation-call metadata for the SEG language.</summary>
public static class DslLiteralSemantics
{
    public static bool IsStringLiteral(DslExpression expression) =>
        expression is LiteralExpression { Kind: "string" or "raw-string" };

    public static string Decode(LiteralExpression literal)
    {
        if (literal.Kind == "raw-string") return literal.Value;
        var text = literal.Value;
        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') text = text.Substring(1, text.Length - 2);
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length) { result.Append(text[i]); continue; }
            var escaped = text[++i];
            result.Append(escaped switch
            {
                '"' => '"', '\\' => '\\', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => escaped
            });
        }
        return result.ToString();
    }

    /// <summary>Applies the existing catalog spelling for embedded double quotes.</summary>
    public static string DecodeForTranslation(LiteralExpression literal)
    {
        var decoded = Decode(literal);
        return literal.Kind == "string" ? decoded.Replace("\"", "''") : decoded;
    }
}

public static class DslNarrativeCallSemantics
{
    private static readonly IReadOnlyDictionary<string, int> TextArgumentByCall =
        new Dictionary<string, int>(StringComparer.Ordinal)
        { ["nar"] = 0, ["narText"] = 0, ["narImg"] = 0, ["narRoom"] = 0 };

    public static bool TryGetTextArgument(CallExpression call, out DslExpression expression)
    {
        if (TextArgumentByCall.TryGetValue(call.Name, out var index) && call.Arguments.Count > index)
        {
            expression = call.Arguments[index].Expression;
            return true;
        }
        expression = null!;
        return false;
    }
}
