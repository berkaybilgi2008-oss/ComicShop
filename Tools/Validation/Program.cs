using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program
{
 static void Main(string[] args)
 {
  int checks=0;
  void Check(bool pass,string message) { if(!pass) throw new Exception(message); checks++; }
  var b = new RecoveryBudget();
  for(int i=0;i<100;i++) { Check(b.Attempt(i*100,3,30),"separate loss "+i); b.ObserveSafe(); b.ObserveSafe(); Check(b.Attempts==0 && b.RetryAt==0,"safe reset"); }
  Check(b.Attempt(10000,3,30),"first");Check(b.Attempt(10001,3,30),"second");Check(b.Attempt(10002,3,30),"third");
  Check(!b.Attempt(10003,3,30),"must back off");Check(b.Attempt(10032,3,30),"must recover after backoff");
  var source = Directory.GetFiles(args[0],"*.cs",SearchOption.AllDirectories).Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:new[]{"UNITY_EDITOR"}),p)).ToArray();
  var syntax=source.SelectMany(t=>t.GetDiagnostics()).Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
  foreach(var d in syntax) Console.WriteLine(d);
  Check(syntax.Length==0,"syntax failed");
  var refs=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(p=>MetadataReference.CreateFromFile(p));
  var compilation=CSharpCompilation.Create("ScopeAudit",source,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
  var codes=new[]{"CS0136","CS0128","CS0102","CS0111","CS0101"};
  var errors=compilation.GetDiagnostics().Where(d=>codes.Contains(d.Id)).ToArray();
  foreach(var d in errors) Console.WriteLine(d);
  Check(errors.Length==0,"duplicate/shadowed declarations");
  Console.WriteLine($"PASS: {checks} checks; RecoveryBudget executed from repository; {source.Length} C# sources parsed with Roslyn C#9 and checked for scope/duplicate declarations. Unity assemblies unavailable: NOT a full Unity compilation.");
 }
}
