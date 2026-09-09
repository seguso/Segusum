using Segusum.Scripting.Core;

namespace Segusum.Translator.Core;

public sealed class DslSourceStringExtractor
{
    public IReadOnlyList<SourceString> Extract(string root, IEnumerable<string>? relativeFiles = null)
    {
        var files = relativeFiles?.ToArray() ?? Directory.EnumerateFiles(root, "*.seg", SearchOption.AllDirectories)
            .Where(x => !x.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or ".git"))
            .Select(x => Path.GetRelativePath(root, x)).ToArray();
        var result = new List<SourceString>();
        foreach (var relativePath in files)
        {
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path)) continue;
            var document = DslParser.Parse(new DslSource(relativePath, File.ReadAllText(path))).Document;
            foreach (var declaration in document.Declarations)
            {
                if (declaration is FunctionDeclaration function) Walk(function.Body, result, relativePath);
                if (declaration is HandlerDeclaration handler)
                {
                    if (handler.Phrase is not null) Add(result, handler.Phrase, relativePath, handler.Phrase.Span.Line);
                    Walk(handler.Body, result, relativePath);
                }
                if (declaration is CycleElementDeclaration cycle) Walk(cycle.Body, result, relativePath);
                if (declaration is BeforeRoomChangeDeclaration beforeRoomChange) Walk(beforeRoomChange.Body, result, relativePath);
                if (declaration is AfterActionExecutedDeclaration afterActionExecuted) Walk(afterActionExecuted.Body, result, relativePath);
                if (declaration is BeforeActionExecutedDeclaration beforeActionExecuted) Walk(beforeActionExecuted.Body, result, relativePath);
                if (declaration is StartGameDeclaration startGame) Walk(startGame.Body, result, relativePath);
            }
        }
        return result.GroupBy(x => x.Value, StringComparer.Ordinal).Select(x => x.First()).ToArray();
    }

    private static void Walk(IEnumerable<DslStatement> body, List<SourceString> result, string path)
    {
        foreach (var statement in body)
        {
            switch (statement)
            {
                case NarStatement narrative: Add(result, narrative.Text, path, narrative.Text.Span.Line); break;
                case NarRoomStatement narrative: Add(result, narrative.Text, path, narrative.Text.Span.Line); break;
                case NarImgStatement narrative: Add(result, narrative.Text, path, narrative.Text.Span.Line); break;
                case DialogueStatement dialogue: Add(result, dialogue.Text, path, dialogue.Text.Span.Line); break;
                case CallStatement { Expression: CallExpression call } when DslNarrativeCallSemantics.TryGetTextArgument(call, out var text):
                    Add(result, text, path, text.Span.Line); break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) Walk(branch.Body, result, path);
                    if (conditional.ElseBody is not null) Walk(conditional.ElseBody, result, path);
                    break;
                case AddCycleElementStatement cycle: Walk(cycle.Body, result, path); break;
                case NamedCutsceneStatement cutscene:
                    Add(result, cutscene.Title, path, cutscene.Title.Span.Line);
                    Walk(cutscene.Body, result, path);
                    break;
            }
        }
    }

    private static void Add(List<SourceString> result, DslExpression expression, string path, int line)
    {
        if (expression is LiteralExpression literal && DslLiteralSemantics.IsStringLiteral(literal))
            result.Add(new SourceString(DslLiteralSemantics.DecodeForTranslation(literal), path, line));
    }
}
