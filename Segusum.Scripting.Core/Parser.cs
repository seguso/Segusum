using System;
using System.Collections.Generic;
using System.Linq;

namespace Segusum.Scripting.Core;

public static class DslParser
{
    public static (DslDocument Document, IReadOnlyList<DslDiagnostic> Diagnostics) Parse(DslSource source)
    {
        var lines = source.Text.Replace("\r", "").Split('\n');
        var declarations = new List<DslDeclaration>();
        var diagnostics = new List<DslDiagnostic>();
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("#")) continue;
            if (raw.StartsWith("state "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(raw, @"^state\s+([^:]+):\s*([A-Za-z0-9_]+)\s*=\s*(.+)$");
                if (!match.Success) { diagnostics.Add(Error("SEGDSL001", "Invalid state declaration.", source.Path, i)); continue; }
                declarations.Add(new StateDeclaration(match.Groups[1].Value.Trim(), match.Groups[2].Value, match.Groups[3].Value.Trim(), source.Path, i + 1));
                continue;
            }
            if (raw.StartsWith("var ") || raw is "next") continue;
            if (raw.StartsWith("def "))
            {
                var header = raw.TrimEnd(':');
                var match = System.Text.RegularExpressions.Regex.Match(header, @"^def\s+([^\s]+)(?:\s+(.+?))?\s+ret\s+([A-Za-z0-9_]+)$");
                if (!match.Success) match = System.Text.RegularExpressions.Regex.Match(header, @"^def\s+([^\s]+)(?:\s+(.+))?$" );
                if (!match.Success) { diagnostics.Add(Error("SEGDSL002", "Invalid def declaration.", source.Path, i)); continue; }
                var body = new List<string>(); var j = i + 1;
                while (j < lines.Length && lines[j].Trim() != "end") { body.Add(lines[j].Trim()); j++; }
                var parameters = ParseParameters(match.Groups[2].Value);
                var ret = match.Groups.Count > 3 && match.Groups[3].Success ? match.Groups[3].Value : null;
                declarations.Add(new FunctionDeclaration(match.Groups[1].Value, parameters, ret, body, source.Path, i + 1)); i = j; continue;
            }
            if (raw.StartsWith("combine ") || raw.StartsWith("use "))
            {
                var kind = raw.StartsWith("combine ") ? "combine" : raw.Contains(" for ") ? "use-for" : "use-here";
                var header = raw.TrimEnd(':');
                var match = kind == "combine"
                    ? System.Text.RegularExpressions.Regex.Match(header, @"^combine\s+(\S+)\s+with\s+(\S+)")
                    : System.Text.RegularExpressions.Regex.Match(header, @"^use\s+(\S+)(?:\s+for\s+(\S+)|\s+here)");
                if (!match.Success) { diagnostics.Add(Error("SEGDSL003", "Invalid handler declaration.", source.Path, i)); continue; }
                var body = new List<string>(); var j = i + 1; while (j < lines.Length && lines[j].Trim() != "end") { body.Add(lines[j].Trim()); j++; }
                var phrase = body.FirstOrDefault(x => x.StartsWith("phrase "))?.Substring(7).Trim();
                var exp = body.FirstOrDefault(x => x.StartsWith("exp "))?.Substring(4).Trim();
                var cond = body.FirstOrDefault(x => x.StartsWith("possible-when "))?.Substring(14).Trim();
                declarations.Add(new HandlerDeclaration(kind, match.Groups[1].Value, kind == "combine" ? match.Groups[2].Value : null, kind == "use-for" ? match.Groups[2].Value : null, phrase, exp, cond, body, source.Path, i + 1)); i = j; continue;
            }
            if (raw.StartsWith("new-cycle")) { declarations.Add(new CycleDeclaration("cyc", source.Path, i + 1)); continue; }
            if (raw.StartsWith("add "))
            {
                var m = System.Text.RegularExpressions.Regex.Match(raw, @"^add\s+(\S+)\s+(\S+)(?:\s+(important))?");
                if (!m.Success) diagnostics.Add(Error("SEGDSL004", "Cycle element ID is required.", source.Path, i));
                else { var body = new List<string>(); var j = i + 1; while (j < lines.Length && lines[j].Trim() != "end") { body.Add(lines[j].Trim()); j++; } declarations.Add(new CycleElementDeclaration(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success, body.FirstOrDefault(x => x.StartsWith("when "))?.Substring(5), body, source.Path, i + 1)); i = j; }
            }
        }
        return (new DslDocument(declarations), diagnostics);
    }
    private static IReadOnlyList<(string Name, string Type)> ParseParameters(string value) => value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => { var text = x.Trim(); var index = text.IndexOf(':'); return (index < 0 ? text : text.Substring(0, index).Trim(), index < 0 ? "object" : text.Substring(index + 1).Trim()); }).ToArray();
    private static DslDiagnostic Error(string id, string message, string path, int line) => new(id, message, path, line + 1, 1);
}
