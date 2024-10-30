namespace Bindgen.NET;

/// <summary>
/// A list of configurations to use for generating bindings.
/// </summary>
public class BindingOptions
{
    /// <summary>
    /// File to process and generate bindings for. <code>Example: "library_header.h"</code>
    /// </summary>
    public string InputFile { get; set; } = "";

    /// <summary>
    /// File path to write generated source code to. If left null, nothing will be outputted to the filesystem. <code>Example: "Library.g.cs"</code>
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// If <see cref="GenerateExternVariables"/> is set to true, a helper C file will be outputted to the filesystem.
    /// This file needs to be compiled alongside the native code that you are generating bindings for.
    /// </summary>
    public string? NativeOutputFile { get; set; }

    /// <summary>
    /// A list of items to skip generating bindings for. This can be used to skip generation for structs, functions, macros, etc.
    /// </summary>
    public List<string> Ignored { get; set; } = new();

    /// <summary>
    /// Include directories for user headers. Declarations found in these headers will have bindings generated for them.
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = new();

    /// <summary>
    /// Include directories for system/standard headers. Declarations found in these headers are ignored by the binding generator.
    /// </summary>
    public List<string> SystemIncludeDirectories { get; set; } = new();

    /// <summary>
    /// Prefixes and their replacement string. An empty replacement string can be used to strip prefixes.
    /// </summary>
    public List<(string prefix, string replacement)> RemappedPrefixes { get; set; } = new();

    /// <summary>
    /// The root namespace of the generated bindings. This defaults to "Bindings"
    /// </summary>
    public string Namespace { get; set; } = "Bindings";

    /// <summary>
    /// The root class of the generated bindings. This defaults to "Native"
    /// </summary>
    public string Class { get; set; } = "Native";

    /// <summary>
    /// Path to a native library that DllImportAttribute will use to load functions. This defaults to "LibraryPath"
    /// </summary>
    public string DllImportPath { get; set; } = "LibraryPath";

    /// <summary>
    /// List of C# warnings to suppress with #pragma. <code>Example: { "CA1069", "CA1401" }</code>
    /// </summary>
    public List<string> SuppressedWarnings { get; set; } = new();

    /// <summary>
    /// Source file name to use for diagnostics when <see cref="TreatInputFileAsRawSourceCode"/> is set to <c>true</c>. This name can be anything and has no effect on generated code. This defaults to "BindgenInputFile.h". <code>Example: "libheader.h"</code>
    /// </summary>
    public string RawSourceName { get; set; } = "BindgenInputFile.h";

    /// <summary>
    /// If set to <c>true</c>, <see cref="InputFile"/> will be treated as raw source code in string form instead of a filepath. It is recommended that you set <see cref="RawSourceName"/> for clearer diagnostics. This defaults to <c>false</c>.
    /// </summary>
    public bool TreatInputFileAsRawSourceCode { get; set; }

    /// <summary>
    /// If set to <c>true</c>, value-like macros will be generated. This defaults to <c>false</c>.
    /// </summary>
    public bool GenerateMacros { get; set; }

    /// <summary>
    /// If set to <c>true</c>, extern variables will be generated.
    /// A native helper file will be generated that must be compiled alongside the code you are generating bindings for.
    /// <see cref="NativeOutputFile"/> should be configured when using this property. This defaults to <c>false</c>.
    /// </summary>
    public bool GenerateExternVariables { get; set; }

    /// <summary>
    /// If set to <c>true</c>, an unmanaged function pointer will be generated instead of an <see cref="IntPtr"/>. This defaults to <c>false</c>.
    /// </summary>
    public bool GenerateFunctionPointers { get; set; }

    /// <summary>
    /// If set to <c>true</c>, all generated method signatures will be marked with the SuppressGCTransition attribute. This defaults to <c>false</c>
    /// </summary>
    public bool GenerateSuppressGcTransition { get; set; }

    /// <summary>
    /// If set to <c>true</c>, all structs will be generated with equality functions and operators. This defaults to <c>false</c>.
    /// </summary>
    /// <returns></returns>
    public bool GenerateStructEqualityFunctions { get; set; }

    /// <summary>
    /// Sets the max diagnostic level to log to the console. This defaults to <c>DiagnosticLevel.Info</c>.
    /// </summary>
    public DiagnosticLevel DiagnosticLevel { get; set; } = DiagnosticLevel.Info;
}
