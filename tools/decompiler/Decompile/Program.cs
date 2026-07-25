using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Decompile <assembly.dll> <output-dir>");
    return 1;
}

string assemblyPath = Path.GetFullPath(args[0]);
string outputDir = Path.GetFullPath(args[1]);

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 1;
}

Console.Error.WriteLine($"Decompiling: {assemblyPath}");
Console.Error.WriteLine($"Output: {outputDir}");

var settings = new DecompilerSettings(LanguageVersion.CSharp7_3)
{
    ThrowOnAssemblyResolveErrors = false,
    RemoveDeadCode = false,
    RemoveDeadStores = false,
    UseDebugSymbols = false,
    ShowXmlDocumentation = false,
    UsingDeclarations = false,
};

var decompiler = new CSharpDecompiler(assemblyPath, settings);

Directory.CreateDirectory(outputDir);

// Write single full file for easy grep searching
Console.Error.WriteLine("Writing FullAssembly.cs (single file)...");
try
{
    var fullCode = decompiler.DecompileWholeModuleAsString();
    File.WriteAllText(Path.Combine(outputDir, "FullAssembly.cs"), fullCode);
    Console.Error.WriteLine($"FullAssembly.cs written ({fullCode.Length} chars)");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FullAssembly failed: {ex.Message}");
}

Console.Error.WriteLine("Done.");
return 0;
