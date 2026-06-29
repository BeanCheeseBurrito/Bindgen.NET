using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ClangSharp;
using ClangSharp.Interop;
using Type = ClangSharp.Type;

namespace Bindgen.NET;

/// <summary>
/// Static class for generating bindings from configuration classes.
/// </summary>
public static class BindingGenerator
{
    private const string MacroPrefix = "BindgenMacro";
    private const string AnonymousPrefix = "Anonymous";

    private static BindingOptions Options = new();

    private static Dictionary<Type, string> TypeNames = new();
    private static Dictionary<Type, string> TypeFullNames = new();

    /// <summary>
    /// Generates bindings based on the values specified in the <c>options</c> parameter.
    /// </summary>
    /// <param name="options">The configuration options to use when generating bindings.</param>
    /// <returns>A string of the generated source code.</returns>
    public static string Generate(BindingOptions options)
    {
        Options = options;

        (TranslationUnit translationUnit, CXIndex index) = ProcessTranslationUnit();

        Cursor[] cursors = translationUnit.TranslationUnitDecl.CursorChildren
            .Where(cursor => cursor is FunctionDecl or RecordDecl or EnumDecl or VarDecl)
            .Where(cursor => !cursor.Location.IsInSystemHeader)
            .Where(IsUserInclude)
            .Where(cursor => !Options.Ignored.Contains(cursor.Handle.Spelling.CString))
            .ToArray();

        IEnumerable<ConstantArrayType> constantArrayTypes = GetConstantArrayTypes(cursors);

        IEnumerable<RecordDecl> recordDecls = GetRecordDecls(cursors);

        IEnumerable<FunctionDecl> functionDecls = cursors
            .OfType<FunctionDecl>()
            .OrderBy(x => x.Name)
            .ToArray();

        IEnumerable<EnumDecl> enumDecls = cursors
            .OfType<EnumDecl>()
            .OrderBy(x => x.Name);

        IEnumerable<VarDecl> varDecls = cursors
            .OfType<VarDecl>()
            .OrderBy(x => x.Name);

        IEnumerable<VarDecl> macroVarDecls = varDecls
            .Where(x => x.Name.StartsWith(MacroPrefix, StringComparison.Ordinal));

        IEnumerable<VarDecl> externVarDecls = varDecls
            .Where(x => x.HasExternalStorage);

        StringBuilder outputBuilder = new();
        StringBuilder nativeOutputBuilder = new();

        foreach (FunctionDecl functionDecl in functionDecls)
            outputBuilder.AppendLine(GenerateFunctionDecl(functionDecl));

        foreach (EnumDecl enumDecl in enumDecls)
            outputBuilder.AppendLine(GenerateEnumDecl(enumDecl));

        foreach (EnumDecl enumDecl in enumDecls)
            outputBuilder.AppendLine(GenerateEnumDeclConstants(enumDecl));

        foreach (RecordDecl recordDecl in recordDecls)
            outputBuilder.AppendLine(GenerateRecordDecl(recordDecl));

        foreach (ConstantArrayType type in constantArrayTypes)
            outputBuilder.AppendLine(GenerateConstantArrayType(type));

        foreach (VarDecl varDecl in macroVarDecls)
            outputBuilder.AppendLine(GenerateMacroVarDecl(varDecl));

        if (Options.GenerateExternVariables)
        {
            foreach (VarDecl varDecl in externVarDecls)
                outputBuilder.AppendLine(GenerateExternVarDeclManagedGetter(varDecl));

            foreach (VarDecl varDecl in externVarDecls)
                outputBuilder.AppendLine(GenerateExternVarDeclField(varDecl));

            foreach (VarDecl varDecl in externVarDecls)
                outputBuilder.AppendLine(GenerateExternVarDeclProperty(varDecl));

            foreach (VarDecl varDecl in externVarDecls)
                nativeOutputBuilder.AppendLine(GenerateExternVarDeclNativeVariable(varDecl));

            foreach (VarDecl varDecl in externVarDecls)
                nativeOutputBuilder.AppendLine(GenerateExternVarDeclNativeGetter(varDecl));
        }

        foreach (RecordDecl recordDecl in recordDecls)
            outputBuilder.AppendLine(GenerateRecordEqualityMethods(recordDecl.TypeForDecl));

        foreach (ConstantArrayType type in constantArrayTypes)
            outputBuilder.AppendLine(GenerateConstantArrayTypeEqualityMethods(type));

        string output = CodeFormatter.Format($$"""
                #nullable enable
                {{(Options.SuppressedWarnings.Count > 0 ? $"#pragma warning disable {string.Join(' ', Options.SuppressedWarnings)}" : string.Empty)}}
                using System;
                using System.Runtime.InteropServices;
                using System.Runtime.CompilerServices;
                
                {{(Options.GenerateDisableRuntimeMarshallingAttribute ? "[assembly: DisableRuntimeMarshalling]" : "")}}
                
                namespace {{Options.Namespace}};
                
                public static unsafe partial class {{Options.Class}}
                {
                    {{GenerateBindgenInternal()}}
                    {{outputBuilder}}
                }
                {{(Options.SuppressedWarnings.Count > 0 ? $"#pragma warning restore {string.Join(' ', Options.SuppressedWarnings)}" : string.Empty)}}
                #nullable disable
            """);

        string nativeOutput = $$"""
            #ifdef _WIN32
                #define BINDGEN_API __declspec(dllexport)
            #else
                #define BINDGEN_API __attribute__((visibility("default")))
            #endif
            {{nativeOutputBuilder}}
            """;


        if (options.OutputFile != null)
        {
            File.WriteAllText(options.OutputFile, output);
            Diagnostic.Log(DiagnosticLevel.Info, $"Generated {Path.GetFullPath(options.OutputFile)} from {GetInputFileName()}");
        }

        if (options.NativeOutputFile != null)
        {
            File.WriteAllText(options.NativeOutputFile, nativeOutput);
            Diagnostic.Log(DiagnosticLevel.Info, $"Generated {Path.GetFullPath(options.NativeOutputFile)} from {GetInputFileName()}");
        }

        translationUnit.Dispose();
        index.Dispose();

        TypeNames = new Dictionary<Type, string>();
        TypeFullNames = new Dictionary<Type, string>();

        return output;
    }

    private static (TranslationUnit, CXIndex) ProcessTranslationUnit()
    {
        Diagnostic.CurrentDiagnosticLevel = Options.DiagnosticLevel;

        string inputFileName = GetInputFileName();

        List<string> arguments = Options.IncludeDirectories
            .Union(Options.SystemIncludeDirectories)
            .Select(includeDirectory => "-I" + Path.GetFullPath(includeDirectory))
            .ToList();

        List<CXUnsavedFile> unsavedFiles = new();
        CXTranslationUnit_Flags flags = CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;

        if (Options.TreatInputFileAsRawSourceCode)
            unsavedFiles.Add(CXUnsavedFile.Create(inputFileName, Options.InputFile));
        else if (!Path.Exists(inputFileName))
            throw new ArgumentException($"Input file at path \"{inputFileName}\" does not exist.", nameof(Options));

        CXIndex index = CXIndex.Create();
        CXErrorCode errorCode = CXTranslationUnit.TryParse(index, inputFileName, arguments.ToArray(), unsavedFiles.ToArray(), flags, out CXTranslationUnit handle);

        foreach (CXUnsavedFile unsavedFile in unsavedFiles)
            unsavedFile.Dispose();

        ProcessDiagnostics(errorCode, handle);

        TranslationUnit translationUnit = TranslationUnit.GetOrCreate(handle);

        translationUnit = ProcessMacros(index, translationUnit, arguments.ToArray(), flags);

        return (translationUnit, index);
    }

    private static string GetInputFileName()
    {
        return Options.TreatInputFileAsRawSourceCode ? Options.RawSourceName : Path.GetFullPath(Options.InputFile);
    }

    // TODO: Handle errors
    private static void ProcessDiagnostics(CXErrorCode errorCode, CXTranslationUnit handle)
    {
        if (handle.NumDiagnostics != 0)
        {
            for (uint i = 0; i < handle.NumDiagnostics; i++)
            {
                using CXDiagnostic diagnostic = handle.GetDiagnostic(i);
                Diagnostic.Log(DiagnosticLevel.Warning, diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
            }
        }
    }

    // We collect all macro definition records and append them to the end of the file as type-inferred auto variables.
    private static TranslationUnit ProcessMacros(CXIndex index, TranslationUnit translationUnit, ReadOnlySpan<string> arguments, CXTranslationUnit_Flags flags)
    {
        string inputFileName = GetInputFileName();
        CXTranslationUnit translationUnitHandle = translationUnit.Handle;

        CXFile file = translationUnitHandle.GetFile(inputFileName);
        ReadOnlySpan<byte> inputFileContents = translationUnitHandle.GetFileContents(file, out UIntPtr _);

        StringBuilder newFileBuilder = new();
        newFileBuilder.AppendLine(Encoding.UTF8.GetString(inputFileContents));

        MacroDefinitionRecord[] macroDefinitionRecords = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<MacroDefinitionRecord>()
            .Where(macro => !macro.Location.IsInSystemHeader)
            .Where(macro => !IsFromNamelessFile(macro))
            .Where(IsUserInclude)
            .ToArray();

        foreach (MacroDefinitionRecord macroDefinitionRecord in macroDefinitionRecords)
            newFileBuilder.AppendLine(GenerateMacroDummy(macroDefinitionRecord));

        translationUnit.Dispose();

        List<CXUnsavedFile> unsavedFiles = new();
        unsavedFiles.Add(CXUnsavedFile.Create(inputFileName, newFileBuilder.ToString()));

        CXTranslationUnit handle = CXTranslationUnit.Parse(index, inputFileName, arguments.ToArray(), unsavedFiles.ToArray(), flags & ~CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord);

        foreach (CXUnsavedFile unsavedFile in unsavedFiles)
            unsavedFile.Dispose();

        return TranslationUnit.GetOrCreate(handle);
    }

    private static string GetSourceRangeContents(CXTranslationUnit translationUnit, CXSourceRange sourceRange)
    {
        sourceRange.Start.GetFileLocation(out CXFile startFile, out uint _, out uint _, out uint startOffset);
        sourceRange.End.GetFileLocation(out CXFile endFile, out uint _, out uint _, out uint endOffset);

        if (startFile != endFile)
            return string.Empty;

        ReadOnlySpan<byte> fileContents = translationUnit.GetFileContents(startFile, out UIntPtr _);
        fileContents = fileContents.Slice(unchecked((int)startOffset), unchecked((int)(endOffset - startOffset)));

        return Encoding.UTF8.GetString(fileContents);
    }

    // We don't want to generate bindings for stuff inside of system includes. We use this to filter for non-system headers.
    private static bool IsUserInclude(Cursor cursor)
    {
        cursor.Location.GetFileLocation(out CXFile file, out _, out _, out _);
        string fileName = file.Name.ToString();

        return Options.SystemIncludeDirectories
            .Select(Path.GetFullPath)
            .All(fullIncludeDirectory => !fileName.StartsWith(fullIncludeDirectory, StringComparison.Ordinal));
    }

    // Don't generate macros from files with empty names because it includes some junk.
    private static bool IsFromNamelessFile(Cursor cursor)
    {
        cursor.Location.GetFileLocation(out CXFile file, out uint _, out uint _, out uint _);
        return string.IsNullOrEmpty(file.Name.ToString());
    }

    private static bool RecordHasDefinition(RecordDecl recordDecl)
    {
        while (!recordDecl.IsThisDeclarationADefinition)
        {
            if (recordDecl.Definition == null)
                return false;

            recordDecl = recordDecl.Definition;
        }

        return true;
    }

    private static RecordDecl GetRecordDefinition(RecordDecl recordDecl)
    {
        while (!recordDecl.IsThisDeclarationADefinition)
        {
            if (recordDecl.Definition == null)
                break;

            recordDecl = recordDecl.Definition;
        }

        return recordDecl;
    }

    private static bool IsType<T>(Type type, [MaybeNullWhen(false)] out T value) where T : Type?
    {
        if (type is T t)
        {
            value = t;
            return true;
        }

        if (type is ElaboratedType elaboratedType)
            return IsType(elaboratedType.CanonicalType, out value);

        if (type is PointerType pointerType)
            return IsType(pointerType.PointeeType, out value);

        if (type is ConstantArrayType constantArrayType)
            return IsType(constantArrayType.ElementType, out value);

        value = default;
        return false;
    }
    private static IEnumerable<RecordDecl> GetRecordDecls(IEnumerable<Cursor> cursors)
    {
        return cursors
            .SelectMany(Recurse)
            .GroupBy(cursor => cursor.Handle.Spelling.CString)
            .Select(group => group.First());

        static IEnumerable<RecordDecl> Recurse(Cursor cursor)
        {
            return new[] { cursor }
                .OfType<RecordDecl>()
                .Concat(cursor.CursorChildren.SelectMany(Recurse));
        }
    }

    private static IEnumerable<ConstantArrayType> GetConstantArrayTypes(IEnumerable<Cursor> cursors)
    {
        return cursors
            .SelectMany(Recurse)
            .Distinct();

        static IEnumerable<ConstantArrayType> Recurse(Cursor cursor)
        {
            return new[] { cursor }
                .OfType<FieldDecl>()
                .Where(field => field.Type is ConstantArrayType)
                .Select(field => (ConstantArrayType)field.Type)
                .Concat(cursor.CursorChildren.SelectMany(Recurse));
        }
    }

    private static string GetCSharpFunctionPointer(FunctionProtoType functionProtoType)
    {
        List<string> parameters = functionProtoType.ParamTypes.Select(GetTypeFullName).ToList();
        parameters.Add(GetTypeFullName(functionProtoType.ReturnType));
        return $"delegate* unmanaged<{string.Join(", ", parameters)}>";
    }

    private static string GetAnonymousName(Cursor cursor, string kind)
    {
        cursor.Location.GetFileLocation(out CXFile file, out uint line, out uint column, out _);
        string fileName = Path.GetFileNameWithoutExtension(file.Name.ToString());
        return $"{AnonymousPrefix}{kind}_{fileName}_L{line}_C{column}";
    }

    private static string GetCursorName(NamedDecl namedDecl)
    {
        string name = GetValidIdentifier(namedDecl.Name);

        if (namedDecl is TypeDecl typeDecl)
        {
            bool isAnonymous =
                string.IsNullOrWhiteSpace(name) ||
                name.StartsWith("struct (unnamed", StringComparison.Ordinal) ||
                name.StartsWith("union (unnamed", StringComparison.Ordinal) ||
                name.StartsWith("enum (unnamed", StringComparison.Ordinal) ||
                name.StartsWith("struct (anonymous", StringComparison.Ordinal) ||
                name.StartsWith("union (anonymous", StringComparison.Ordinal) ||
                name.StartsWith("enum (anonymous", StringComparison.Ordinal) ||
                name.StartsWith("(unnamed struct", StringComparison.Ordinal) ||
                name.StartsWith("(unnamed union", StringComparison.Ordinal) ||
                name.StartsWith("(unnamed enum", StringComparison.Ordinal);

            return isAnonymous ? GetAnonymousName(typeDecl, typeDecl.TypeForDecl.KindSpelling) : name;
        }

        return name;
    }

    private static string GetSignedIntegerName(long size, string? error = null)
    {
        return size switch
        {
            1 => "sbyte",
            2 => "short",
            4 => "int",
            8 => "long",
            _ => error ?? "INVALID_SIGNED_INTEGER"
        };
    }

    private static string GetUnsignedIntegerName(long size, string? error = null)
    {
        return size switch
        {
            1 => "byte",
            2 => "ushort",
            4 => "uint",
            8 => "ulong",
            _ => error ?? "INVALID_UNSIGNED_INTEGER"
        };
    }

    private static string GetIntegerName(long size, bool signed, string? error = null)
    {
        return signed ? GetSignedIntegerName(size, error) : GetUnsignedIntegerName(size, error);
    }

    private static string GetTypeName(Type? type)
    {
        if (type == null)
            return "UNHANDLED_TYPE";

        if (TypeNames.TryGetValue(type, out string? typeName))
            return typeName;

        if (type is RecordType recordType)
            typeName = GetCursorName(recordType.Decl);

        if (type is EnumType enumType)
            typeName = GetCursorName(enumType.Decl);

        if (type is AutoType autoType)
            typeName = GetTypeName(autoType.GetDeducedType);

        if (type is ConstantArrayType constantArrayType)
            typeName = $"{GetTypeIdentifier(constantArrayType.ElementType)}_{constantArrayType.Size}";

        if (type is FunctionProtoType functionProtoType)
            typeName = GetCSharpFunctionPointer(functionProtoType);

        if (type is IncompleteArrayType incompleteArrayType)
            typeName = GetTypeName(incompleteArrayType.ElementType) + "*";

        if (type is PointerType pointerType)
        {
            if (pointerType.CanonicalType.PointeeType is FunctionProtoType)
                typeName = GetTypeName(pointerType.PointeeType);
            else if (pointerType.PointeeType.AsString is "FILE" or "_IO_FILE"
                || (pointerType.PointeeType.CanonicalType is RecordType fileRecordType && fileRecordType.Decl.Name == "_IO_FILE"))
                typeName = "void*";
            else
                typeName = GetTypeName(pointerType.CanonicalType.PointeeType) + "*";
        }

        if (type is ElaboratedType elaboratedType)
        {
            typeName = elaboratedType.AsString switch
            {
                "size_t" => "nint",
                "va_list" => "void*",
                _ => GetTypeName(elaboratedType.CanonicalType)
            };
        }

        if (type is BuiltinType builtinType)
        {
            typeName = builtinType.Kind switch
            {
                CXTypeKind.CXType_Bool => Options.GenerateDisableRuntimeMarshallingAttribute ? "bool" : "byte",
                CXTypeKind.CXType_Float => "float",
                CXTypeKind.CXType_Double => "double",
                CXTypeKind.CXType_LongDouble => "decimal",
                CXTypeKind.CXType_Void => "void",
                CXTypeKind.CXType_Char16 or
                CXTypeKind.CXType_Char32 or
                CXTypeKind.CXType_Char_S or
                CXTypeKind.CXType_Char_U or
                CXTypeKind.CXType_SChar or
                CXTypeKind.CXType_UChar or
                CXTypeKind.CXType_WChar => GetIntegerName(builtinType.Handle.SizeOf, builtinType.Handle.IsSigned, $"INVALID_CHAR_{builtinType.Kind}"),
                CXTypeKind.CXType_Short or
                CXTypeKind.CXType_Int or
                CXTypeKind.CXType_Long or
                CXTypeKind.CXType_LongLong => GetSignedIntegerName(builtinType.Handle.SizeOf, $"INVALID_SIGNED_INTEGER_{builtinType.Kind}_SIZEOF_{builtinType.Handle.SizeOf}"),
                CXTypeKind.CXType_UShort or
                CXTypeKind.CXType_UInt or
                CXTypeKind.CXType_ULong or
                CXTypeKind.CXType_ULongLong => GetUnsignedIntegerName(builtinType.Handle.SizeOf, $"INVALID_UNSIGNED_INTEGER_{builtinType.Kind}_SIZEOF_{builtinType.Handle.SizeOf}"),
                _ => $"INVALID_BUILTIN_{builtinType.Kind}"
            };
        }

        TypeNames.Add(type, typeName ??= "UNHANDLED_TYPE");
        return typeName;
    }

    private static string GetTypeFullName(Type? type)
    {
        if (type == null)
            return "UNHANDLED_TYPE";

        if (TypeFullNames.TryGetValue(type, out string? typeName))
            return typeName;

        typeName = GetTypeName(type);

        if (IsType<RecordType>(type, out RecordType? recordType) && recordType.Decl.Parent is RecordDecl parent)
            typeName = $"{GetTypeFullName(parent.TypeForDecl)}.{typeName}";

        if (type is ConstantArrayType)
            typeName = $"InlineArrays.{typeName}";

        TypeFullNames.Add(type, typeName);
        return typeName;
    }

    private static string GetValidIdentifier(string identifier, bool remap = true)
    {
        if (remap)
        {
            foreach ((string prefix, string replacement) in Options.RemappedPrefixes)
            {
                if (!identifier.StartsWith(prefix, StringComparison.InvariantCulture))
                    continue;

                identifier = replacement + identifier[prefix.Length..];
                break;
            }
        }

        switch (identifier)
        {
            case "abstract":
            case "as":
            case "base":
            case "bool":
            case "break":
            case "byte":
            case "case":
            case "catch":
            case "char":
            case "checked":
            case "class":
            case "const":
            case "continue":
            case "decimal":
            case "default":
            case "delegate":
            case "do":
            case "double":
            case "else":
            case "enum":
            case "event":
            case "explicit":
            case "extern":
            case "false":
            case "finally":
            case "fixed":
            case "float":
            case "for":
            case "foreach":
            case "goto":
            case "if":
            case "implicit":
            case "in":
            case "int":
            case "interface":
            case "internal":
            case "is":
            case "lock":
            case "long":
            case "namespace":
            case "new":
            case "null":
            case "object":
            case "operator":
            case "out":
            case "override":
            case "params":
            case "private":
            case "protected":
            case "public":
            case "readonly":
            case "ref":
            case "return":
            case "sbyte":
            case "sealed":
            case "short":
            case "sizeof":
            case "stackalloc":
            case "static":
            case "string":
            case "struct":
            case "switch":
            case "this":
            case "throw":
            case "true":
            case "try":
            case "typeof":
            case "uint":
            case "ulong":
            case "unchecked":
            case "unsafe":
            case "ushort":
            case "using":
            case "virtual":
            case "void":
            case "volatile":
            case "while":
                return "@" + identifier;
            default:
                return identifier;
        }
    }

    private static string GenerateExternFieldName(string name)
    {
        return name + "_Ptr";
    }

    private static string GenerateExternGetterName(string name)
    {
        return name + "_BindgenGetExtern";
    }

    private static string GenerateBindgenInternal()
    {
        return $$"""
            public partial class BindgenInternal
            {
                public const string DllImportPath = @"{{Options.DllImportPath}}";
            }
        """;
    }

    // Generates outer declarations for nested types.
    private static string GenerateOuterDeclarations(Type type, string body)
    {
        string name = GetTypeFullName(type);
        string[] outerTypes = name.Split('.')[..^1];
        return outerTypes.Length == 0 ? body : $$"""
            {{string.Concat(outerTypes.Select(outerType => $"public partial struct {outerType}{{"))}}
            {{body}}
            {{new string('}', outerTypes.Length)}}
            """;
    }

    private static string GenerateFunctionDecl(FunctionDecl functionDecl)
    {
        IEnumerable<string> parameters = functionDecl.Parameters
            .Select(parameter => $"{GetTypeFullName(parameter.Type)} {GetValidIdentifier(parameter.Name)}");

        return $@"
            {(Options.GenerateSuppressGcTransition ? "[SuppressGCTransition]" : string.Empty)}
            [DllImport(BindgenInternal.DllImportPath, EntryPoint = ""{GetValidIdentifier(functionDecl.NameInfoName, false)}"")]
            public static extern {GetTypeFullName(functionDecl.ReturnType)} {GetValidIdentifier(functionDecl.Name)}({string.Join(", ", parameters)});
        ";
    }

    private static string GenerateConstantArrayType(ConstantArrayType type)
    {
        string name = GetTypeName(type.ElementType);
        string structName = GetTypeName(type);
        return GenerateOuterDeclarations(type, $$"""
            [InlineArray({{type.Size}})]
            public partial struct {{structName}}
            {
                public {{name}} Item0;
            }
            """);
    }

    private static string GenerateConstantArrayTypeEqualityMethods(ConstantArrayType type)
    {
        string name = GetTypeName(type);
        return GenerateOuterDeclarations(type, $$"""
            public partial struct {{name}} : IEquatable<{{name}}>
            {
                {{GenerateRecordEqualityFunctions(name)}}
            }
            """);
    }

    private static string GenerateRecordDecl(RecordDecl recordDecl)
    {
        if (RecordHasDefinition(recordDecl) && !recordDecl.IsThisDeclarationADefinition)
            return GenerateRecordDecl(GetRecordDefinition(recordDecl));

        string recordName = GetTypeName(recordDecl.TypeForDecl);

        IEnumerable<FieldDecl> fieldsDecls = recordDecl.Decls.OfType<FieldDecl>();
        IEnumerable<IndirectFieldDecl> indirectFieldDecls = recordDecl.Decls.OfType<IndirectFieldDecl>();

        StringBuilder fields = new();

        foreach (FieldDecl fieldDecl in fieldsDecls)
        {
            if (recordDecl.IsUnion)
                fields.AppendLine("[System.Runtime.InteropServices.FieldOffset(0)]");

            string fieldName = GetValidIdentifier(fieldDecl.Name);
            string typeName = GetTypeFullName(fieldDecl.Type);

            if (fieldDecl.IsAnonymousField)
                fieldName = typeName.Replace('.', '_') + "_Field";

            fields.AppendLine(CultureInfo.InvariantCulture, $"public {typeName} {fieldName};");
        }

        foreach (IndirectFieldDecl indirectFieldDecl in indirectFieldDecls)
        {
            IDeclContext declContext = indirectFieldDecl.AnonField.DeclContext!;

            if (declContext is RecordDecl contextRecordDecl)
            {
                string typeName = GetTypeFullName(indirectFieldDecl.Type);
                string declContextName = GetTypeFullName(contextRecordDecl.TypeForDecl).Replace('.', '_') + "_Field";
                string fieldName = GetValidIdentifier(indirectFieldDecl.Name);

                fields.AppendLine(CultureInfo.InvariantCulture,
                    $"public ref {typeName} {fieldName} => ref {declContextName}.{fieldName};");
            }
        }

        return GenerateOuterDeclarations(recordDecl.TypeForDecl, $$"""
            {{(recordDecl.IsUnion ? "[StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]" : "")}}
            public partial struct {{recordName}}
            {
                {{fields}}
            }
            """);
    }

    private static string GenerateRecordEqualityMethods(Type record)
    {
        string recordName = GetTypeName(record);

        return GenerateOuterDeclarations(record, $$"""
            public partial struct {{recordName}} : IEquatable<{{recordName}}>
            {
                {{GenerateRecordEqualityFunctions(recordName)}}
            }
            """);
    }

    private static string GenerateEnumDecl(EnumDecl enumDecl)
    {
        List<string> enumMembers = new ();
        bool hasNegatives = false;

        foreach (EnumConstantDecl enumConstant in enumDecl.Enumerators)
        {
            if (enumConstant.IsNegative)
                hasNegatives = true;

            enumMembers.Add(GenerateEnumConstantDecl(enumConstant));
        }

        return GenerateOuterDeclarations(enumDecl.TypeForDecl, $@"
            public enum {GetTypeName(enumDecl.TypeForDecl)} : {GetIntegerName(enumDecl.IntegerType.Handle.SizeOf, hasNegatives, "INVALID_ENUM_INTEGER")}
            {{ 
                {string.Join(",\n", enumMembers)}
            }}
        ");
    }

    private static string GenerateEnumConstantDecl(EnumConstantDecl enumConstantDecl)
    {
        string value = enumConstantDecl.IsSigned
            ? enumConstantDecl.InitVal.ToString(CultureInfo.InvariantCulture)
            : enumConstantDecl.UnsignedInitVal.ToString(CultureInfo.InvariantCulture);

        return $"{GetValidIdentifier(enumConstantDecl.Name)} = {value}";
    }

    private static string GenerateEnumDeclConstants(EnumDecl enumDecl)
    {
        string enumName = GetTypeFullName(enumDecl.TypeForDecl);

        IEnumerable<string> constantFields = enumDecl.Decls.OfType<EnumConstantDecl>().Select(enumConstantDecl =>
        {
            string enumMemberName = GetValidIdentifier(enumConstantDecl.Name);
            return $"public const {enumName} {enumMemberName} = {enumName}.{enumMemberName};";
        });

        return string.Join("\n", constantFields);
    }

    private static string GenerateMacroVarDecl(VarDecl varDecl)
    {
        if (!varDecl.Name.StartsWith(MacroPrefix, StringComparison.Ordinal))
            return string.Empty;

        if (!varDecl.HasInit)
            return string.Empty;

        Expr init = varDecl.Init;
        CXEvalResult result = varDecl.Handle.Evaluate;

        string typeName = GetTypeFullName(varDecl.Type);
        string expression;

        switch (result.Kind)
        {
            case CXEvalResultKind.CXEval_Float:
                expression = init.Type.Kind switch
                {
                    CXTypeKind.CXType_Double =>  result.AsDouble.ToString(CultureInfo.InvariantCulture),
                    CXTypeKind.CXType_Float =>  ((float)result.AsDouble).ToString(CultureInfo.InvariantCulture) + "f",
                    CXTypeKind.CXType_LongDouble =>  ((decimal)result.AsDouble).ToString(CultureInfo.InvariantCulture),
                    _ => $"INVALID_FLOAT_{init.Type.Kind}"
                };
                break;
            case CXEvalResultKind.CXEval_Int:
                expression = init.Type.Handle.SizeOf switch
                {
                    1 => result.IsUnsignedInt ? ((byte)result.AsUnsigned).ToString(CultureInfo.InvariantCulture) : ((sbyte)result.AsLongLong).ToString(CultureInfo.InvariantCulture),
                    2 => result.IsUnsignedInt ? ((ushort)result.AsUnsigned).ToString(CultureInfo.InvariantCulture) : ((short)result.AsLongLong).ToString(CultureInfo.InvariantCulture),
                    4 => result.IsUnsignedInt ? ((uint)result.AsUnsigned).ToString(CultureInfo.InvariantCulture) : ((int)result.AsLongLong).ToString(CultureInfo.InvariantCulture),
                    8 => result.IsUnsignedInt ? result.AsUnsigned.ToString(CultureInfo.InvariantCulture) : result.AsLongLong.ToString(CultureInfo.InvariantCulture),
                    _ => $"INVALID_INTEGER_SIZEOF_{init.Type.Handle.SizeOf}"
                };
                break;
            case CXEvalResultKind.CXEval_StrLiteral:
                typeName = "string";
                expression = "\"" + result.AsStr + "\"";
                break;
            case CXEvalResultKind.CXEval_ObjCStrLiteral:
            case CXEvalResultKind.CXEval_CFStr:
            case CXEvalResultKind.CXEval_Other:
            case CXEvalResultKind.CXEval_UnExposed:
            default:
                return "";
        }

        return string.IsNullOrEmpty(expression)
            ? string.Empty
            : $"public const {typeName} {GetValidIdentifier(varDecl.Name[MacroPrefix.Length..])} = {expression};";
    }

    private static string GenerateExternVarDeclNativeVariable(VarDecl varDecl)
    {
        return $$"""
            extern void* {{varDecl.Name}};
            """;
    }

    private static string GenerateExternVarDeclNativeGetter(VarDecl varDecl)
    {
        return $$"""
        BINDGEN_API void* {{GenerateExternGetterName(varDecl.Name)}}() {
            return &{{varDecl.Name}};
        }
        """;
    }

    private static string GenerateExternVarDeclManagedGetter(VarDecl varDecl)
    {
        string validName = GetValidIdentifier(varDecl.Name);
        return $$"""
            [DllImport(BindgenInternal.DllImportPath, EntryPoint = "{{GenerateExternGetterName(varDecl.Name)}}")]
            private static extern void* {{GenerateExternGetterName(validName)}}();
            """;
    }

    private static string GenerateExternVarDeclField(VarDecl varDecl)
    {
        string validName = GetValidIdentifier(varDecl.Name);
        string fieldName = GenerateExternFieldName(validName);
        return varDecl.HasExternalStorage ? $"private static void* {fieldName};" : "";
    }

    private static string GenerateExternVarDeclProperty(VarDecl varDecl)
    {
        if (!varDecl.HasExternalStorage)
            return "";

        string typeName = GetTypeFullName(varDecl.Type);
        string validName = GetValidIdentifier(varDecl.Name);
        string fieldName = GenerateExternFieldName(validName);
        string getterName = GenerateExternGetterName(validName);

        // We can't use Unsafe.AsRef<T>(void*) because T can't be a pointer.
        return $"public static ref {typeName} {validName} => ref *({typeName}*)({fieldName} == null ? {fieldName} = {getterName}() : {fieldName});";
    }

    // This converts value-like macros to type-inferred variables so we can get access to it's type information.
    // The macro's constants will be generated in GenerateMacroVarDecl().
    private static string GenerateMacroDummy(MacroDefinitionRecord macro)
    {
        if (macro.IsFunctionLike)
            return string.Empty;

        CXTranslationUnit translationUnitHandle = macro.TranslationUnit.Handle;
        Span<CXToken> tokens = translationUnitHandle.Tokenize(macro.Extent);

        bool hasNoValue = tokens[0].Kind != CXTokenKind.CXToken_Identifier ||
                          tokens[0].GetSpelling(translationUnitHandle).CString != macro.Spelling ||
                          tokens.Length == 1;

        if (hasNoValue)
            return string.Empty;

        CXSourceLocation sourceRangeEnd = tokens[^1].GetExtent(translationUnitHandle).End;
        CXSourceLocation sourceRangeStart = tokens[1].GetLocation(translationUnitHandle);
        CXSourceRange sourceRange = CXSourceRange.Create(sourceRangeStart, sourceRangeEnd);

        string value = GetSourceRangeContents(translationUnitHandle, sourceRange);

        return $"const __auto_type {MacroPrefix}{macro.Name} = {value};";
    }

    /// <summary>
    /// Returns <see cref="GetTypeName"/> mapped to a valid C# identifier by
    /// replacing '*' with 'P' and every other non-identifier character with
    /// '_', so array-of-function-pointer types (e.g. "delegate* unmanaged&lt;...&gt;")
    /// yield a usable struct name.
    /// </summary>
    private static string GetTypeIdentifier(Type type)
    {
        string name = GetTypeName(type);

        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(c == '*' ? 'P' : char.IsLetterOrDigit(c) || c == '_' ? c : '_');

        return sb.ToString();
    }

    private static string GenerateRecordEqualityFunctions(string recordName)
    {
        return $$"""
            public bool Equals({{recordName}} other)
            {
                fixed ({{recordName}}* __self = &this)
                {
                    return new Span<byte>(__self, sizeof({{recordName}})).SequenceEqual(new Span<byte>(&other, sizeof({{recordName}})));
                }
            }

            public override bool Equals(object? obj)
            {
                return obj is {{recordName}} other && Equals(other);
            }

            public static bool operator ==({{recordName}} left, {{recordName}} right)
            {
                return left.Equals(right);
            }

            public static bool operator !=({{recordName}} left, {{recordName}} right)
            {
                return !(left == right);
            }

            public override int GetHashCode()
            {
                fixed ({{recordName}}* __self = &this)
                {
                    HashCode hash = new();
                    hash.AddBytes(new Span<byte>(__self, sizeof({{recordName}})));
                    return hash.ToHashCode();
                }
            }
            """;
    }
}
