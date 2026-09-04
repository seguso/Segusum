using System;
using System.Collections.Generic;
namespace Segusum.Scripting.Core;
public enum DslTokenKind { Identifier, Number, String, NewLine, Semicolon, Colon, Comma, LParen, RParen, Operator, EndOfFile }
public readonly record struct DslToken(DslTokenKind Kind, string Text, SourceSpan Span);
public static class DslLexer
{
 public static IReadOnlyList<DslToken> Lex(DslSource source, List<DslDiagnostic> diagnostics)
 {
  var r=new List<DslToken>();var t=source.Text;var i=0;
  while(i<t.Length){var s=i;var c=t[i];
   if(c==' '||c=='\t'||c=='\r'){i++;continue;}
   if(c=='#'){while(i<t.Length&&t[i]!='\n')i++;continue;}
   if(c=='\n'){r.Add(new(DslTokenKind.NewLine,"\n",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c==';'){r.Add(new(DslTokenKind.Semicolon,";",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c==':'){r.Add(new(DslTokenKind.Colon,":",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c==','){r.Add(new(DslTokenKind.Comma,",",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c=='('){r.Add(new(DslTokenKind.LParen,"(",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c==')'){r.Add(new(DslTokenKind.RParen,")",SourceSpan.From(source.Path,t,i,1)));i++;continue;}
   if(c=='"'){i++;while(i<t.Length&&t[i]!='"')i+=t[i]=='\\'&&i+1<t.Length?2:1;if(i>=t.Length){diagnostics.Add(new("SEGDSL100","Unterminated string literal.",SourceSpan.From(source.Path,t,s,1)));break;}i++;r.Add(new(DslTokenKind.String,t.Substring(s,i-s),SourceSpan.From(source.Path,t,s,i-s)));continue;}
   if(char.IsDigit(c)){while(i<t.Length&&(char.IsDigit(t[i])||t[i]=='.'))i++;r.Add(new(DslTokenKind.Number,t.Substring(s,i-s),SourceSpan.From(source.Path,t,s,i-s)));continue;}
   if(char.IsLetter(c)||c=='_'){while(i<t.Length&&(char.IsLetterOrDigit(t[i])||t[i]=='_'||t[i]=='-'))i++;r.Add(new(DslTokenKind.Identifier,t.Substring(s,i-s),SourceSpan.From(source.Path,t,s,i-s)));continue;}
   var op=c.ToString();i++;if(i<t.Length&&"=+<>".IndexOf(t[i])>=0&&(c=='='||c=='+'||c=='<'||c=='>'))op+=t[i++];r.Add(new(DslTokenKind.Operator,op,SourceSpan.From(source.Path,t,s,i-s)));
  }
  r.Add(new(DslTokenKind.EndOfFile,"",SourceSpan.From(source.Path,t,t.Length,0)));return r;
 }
}
