using System;
using System.Collections.Generic;
namespace Segusum.Scripting.Core;
public enum DslTokenKind { Identifier, Number, String, NewLine, Semicolon, Colon, Comma, LParen, RParen, LBracket, RBracket, Operator, EndOfFile }
public readonly record struct DslToken(DslTokenKind Kind, string Text, SourceSpan Span);
public static class DslLexer
{
 public static IReadOnlyList<DslToken> Lex(DslSource source, List<DslDiagnostic> diagnostics)
 {
  var r=new List<DslToken>();var t=source.Text;var i=0;var line=1;var column=1;
  var profile = DslParser.ActiveProfile;
  var loopStarted = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
  while(i<t.Length){var s=i;var c=t[i];
   var tokenLine=line;var tokenColumn=column;
   if(c==' '||c=='\t'||c=='\r'){Advance(c);continue;}
   if(c=='#'||(c=='/'&&i+1<t.Length&&t[i+1]=='/')){while(i<t.Length&&t[i]!='\n')Advance(t[i]);continue;}
   if(c=='\n'){r.Add(new(DslTokenKind.NewLine,"\n",new SourceSpan(source.Path,i,1,line,column)));Advance(c);continue;}
   if(c==';'){r.Add(new(DslTokenKind.Semicolon,";",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c==':'){r.Add(new(DslTokenKind.Colon,":",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c==','){r.Add(new(DslTokenKind.Comma,",",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c=='('){r.Add(new(DslTokenKind.LParen,"(",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c==')'){r.Add(new(DslTokenKind.RParen,")",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c=='['){r.Add(new(DslTokenKind.LBracket,"[",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c==']'){r.Add(new(DslTokenKind.RBracket,"]",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));Advance(c);continue;}
   if(c=='"'){Advance(c);while(i<t.Length&&t[i]!='"'){var current=t[i];Advance(current);if(current=='\\'&&i<t.Length)Advance(t[i]);}if(i>=t.Length){diagnostics.Add(new("SEGDSL100","Unterminated string literal.",new SourceSpan(source.Path,s,1,tokenLine,tokenColumn)));break;}Advance(t[i]);r.Add(new(DslTokenKind.String,t.Substring(s,i-s),new SourceSpan(source.Path,s,i-s,tokenLine,tokenColumn)));continue;}
   if(char.IsDigit(c)){while(i<t.Length&&(char.IsDigit(t[i])||t[i]=='.'))Advance(t[i]);r.Add(new(DslTokenKind.Number,t.Substring(s,i-s),new SourceSpan(source.Path,s,i-s,tokenLine,tokenColumn)));continue;}
   if(char.IsLetter(c)||c=='_'){while(i<t.Length&&(char.IsLetterOrDigit(t[i])||t[i]=='_'||t[i]=='-'))Advance(t[i]);r.Add(new(DslTokenKind.Identifier,t.Substring(s,i-s),new SourceSpan(source.Path,s,i-s,tokenLine,tokenColumn)));continue;}
   var op=c.ToString();Advance(c);if(i<t.Length&&"=+<>".IndexOf(t[i])>=0&&(c=='='||c=='+'||c=='<'||c=='>'||c=='!')){op+=t[i];Advance(t[i]);}r.Add(new(DslTokenKind.Operator,op,new SourceSpan(source.Path,s,i-s,tokenLine,tokenColumn)));
  }
  if (profile != null) profile.AddPhase("lexer-tokenization-loop", System.Diagnostics.Stopwatch.GetTimestamp() - loopStarted);
  var eofStarted = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
  r.Add(new(DslTokenKind.EndOfFile,"",new SourceSpan(source.Path,t.Length,0,line,column)));
  if (profile != null) profile.AddPhase("token-list-finalization", System.Diagnostics.Stopwatch.GetTimestamp() - eofStarted);
  return r;

  void Advance(char value)
  {
   i++;
   if(value=='\n'){line++;column=1;}else column++;
  }
 }
}
