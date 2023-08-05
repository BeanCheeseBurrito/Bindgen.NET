using System.Globalization;
using System.Text;
using ClangSharp;
using ClangSharp.Interop;
using Type = ClangSharp.Type;

namespace Bindgen.NET;

public static class BindingGenerator
{
    private const string MacroPrefix = "_BindgenMacro_";

    private static BindingOptions _options = new ();

    /// <summary>
    /// Generates bindings for all configurations given.
    /// </summary>
    /// <param name="options">An array of configuration options to use when generating bindings.</param>
    /// <returns>An array containing strings of generated source code for all binding configurations.</returns>
    public static KeyValuePair<string, string>[] Generate(ReadOnlySpan<BindingOptions> options)
    {
        BindingOptions[] optionsArray = options.ToArray();

        return optionsArray
            .Select(option => new KeyValuePair<string, string>(option.OutputFile, Generate(option)))
            .ToArray();
    }

    /// <summary>
    /// Generates bindings based on the values specified in the <see cref="options"/> parameter.
    /// </summary>
    /// <param name="options">The configuration options to use when generating bindings.</param>
    /// <returns>A string of the generated source code.</returns>
    public static string Generate(BindingOptions options)
    {
        _options = options;

        string inputFileName = GetInputFileName();

        List<string> arguments = options.IncludeDirectories
            .Union(options.SystemIncludeDirectories)
            .Select(includeDirectory => "-I" + Path.GetFullPath(includeDirectory))
            .ToList();

        List<CXUnsavedFile> unsavedFiles = new();
        CXTranslationUnit_Flags flags = default;

        if (options.GenerateMacros)
            flags |= CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;

        if (options.TreatInputFileAsRawSourceCode)
            unsavedFiles.Add(CXUnsavedFile.Create(inputFileName, options.InputFile));
        else if (!Path.Exists(inputFileName))
            throw new ArgumentException($"Input file at path \"{inputFileName}\" does not exist.", nameof(options));

        CXIndex index = CXIndex.Create();
        CXErrorCode errorCode = CXTranslationUnit.TryParse(index, inputFileName, arguments.ToArray(), unsavedFiles.ToArray(), flags, out CXTranslationUnit handle);

        ProcessDiagnostics(errorCode, handle);

        TranslationUnit translationUnit = TranslationUnit.GetOrCreate(handle);

        if (options.GenerateMacros)
            translationUnit = ProcessMacros(index, translationUnit, arguments.ToArray(), flags);

        string output = GenerateTranslationUnitDecl(translationUnit.TranslationUnitDecl);
        string formattedOutput = CodeFormatter.Format(output);

        if (options.GenerateToFilesystem)
            File.WriteAllText(options.OutputFile, formattedOutput);

        translationUnit.Dispose();
        index.Dispose();
        foreach (CXUnsavedFile unsavedFile in unsavedFiles)
            unsavedFile.Dispose();

        return formattedOutput;
    }

    private static string GetInputFileName()
    {
        return _options.TreatInputFileAsRawSourceCode ? _options.RawSourceName : _options.InputFile;
    }

    // TODO: Handle errors
    private static void ProcessDiagnostics(CXErrorCode errorCode, CXTranslationUnit handle)
    {
        if (handle.NumDiagnostics != 0)
        {
            for (uint i = 0; i < handle.NumDiagnostics; i++)
            {
                using CXDiagnostic diagnostic = handle.GetDiagnostic(i);
                Console.WriteLine(diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
            }
        }
    }

    // We collect all macro definition records and append them to the end of the file as type-inferred auto variables.
    private static TranslationUnit ProcessMacros(CXIndex index, TranslationUnit translationUnit, ReadOnlySpan<string> arguments, CXTranslationUnit_Flags flags)
    {
        string inputFileName = GetInputFileName();
        CXTranslationUnit translationUnitHandle = translationUnit.Handle;

        CXFile file = translationUnitHandle.GetFile(inputFileName);
        ReadOnlySpan<byte> fileContents = translationUnitHandle.GetFileContents(file, out UIntPtr _);

        StringBuilder newFileBuilder = new();
        newFileBuilder.AppendLine(Encoding.UTF8.GetString(fileContents));

        MacroDefinitionRecord[] macroDefinitionRecords = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<MacroDefinitionRecord>()
            .Where(macro => !macro.Location.IsInSystemHeader)
            .Where(macro => !IsFromNamelessFile(macro))
            .Where(IsUserInclude)
            .ToArray();

        foreach (MacroDefinitionRecord macroDefinitionRecord in macroDefinitionRecords)
            newFileBuilder.AppendLine(GenerateMacroDummy(macroDefinitionRecord));

        using CXUnsavedFile unsavedFile = CXUnsavedFile.Create(inputFileName, newFileBuilder.ToString());
        CXUnsavedFile[] unsavedFiles = { unsavedFile };

        translationUnit.Dispose();

        CXTranslationUnit handle = CXTranslationUnit.Parse(index, inputFileName, arguments.ToArray(), unsavedFiles, flags & ~CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord);
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
        return _options.SystemIncludeDirectories
            .Select(Path.GetFullPath)
            .All(fullIncludeDirectory => !fileName.StartsWith(fullIncludeDirectory, StringComparison.Ordinal));
    }

    // TODO: Are there cases where nameless files are fine?
    // Don't generate macros from files with empty names because it includes some junk.
    private static bool IsFromNamelessFile(Cursor cursor)
    {
        cursor.Location.GetFileLocation(out CXFile file, out uint _, out uint _, out uint _);
        return string.IsNullOrEmpty(file.Name.ToString());
    }

    // Don't use prefixes because it's noisy and name could possibly start with an @
    private static string GenerateExternFieldName(string name)
    {
        return name + "_PTR";
    }

    private static string GenerateTranslationUnitDecl(TranslationUnitDecl translationUnitDecl)
    {
        Cursor[] cursors = translationUnitDecl.CursorChildren
            .Where(cursor => cursor is LinkageSpecDecl or FunctionDecl or RecordDecl or EnumDecl or VarDecl)
            .Where(cursor => !cursor.Location.IsInSystemHeader)
            .Where(IsUserInclude)
            .ToArray();

        FunctionDecl[] functionDecls = cursors
            .OfType<FunctionDecl>()
            .OrderBy(x => x.Name)
            .ToArray();

        RecordDecl[] recordDecls = cursors
            .OfType<RecordDecl>()
            .Where(recordDecl => recordDecl.Decls.Count != 0)
            .OrderBy(x => x.Name)
            .ToArray();

        RecordDecl[] opaqueRecordDecls = cursors
            .OfType<RecordDecl>()
            .Where(recordDecl => recordDecl.Decls.Count == 0)
            .OrderBy(x => x.Name)
            .ToArray();

        EnumDecl[] enumDecls = cursors
            .OfType<EnumDecl>()
            .OrderBy(x => x.Name)
            .ToArray();

        VarDecl[] varDecls = cursors
            .OfType<VarDecl>()
            .OrderBy(x => x.Name)
            .ToArray();

        VarDecl[] macroVarDecls = varDecls
            .Where(x => x.Name.StartsWith(MacroPrefix, StringComparison.Ordinal))
            .ToArray();

        VarDecl[] externVarDecls = varDecls
            .Where(x => x.HasExternalStorage)
            .ToArray();

        StringBuilder output = new();

        foreach (FunctionDecl functionDecl in functionDecls)
            output.AppendLine(GenerateFunctionDecl(functionDecl));

        foreach (RecordDecl recordDecl in recordDecls)
            output.AppendLine(GenerateRecordDecl(recordDecl));

        foreach (RecordDecl recordDecl in opaqueRecordDecls)
            output.AppendLine(GenerateRecordDecl(recordDecl));

        foreach (EnumDecl enumDecl in enumDecls)
            output.AppendLine(GenerateEnumDecl(enumDecl));

        foreach (EnumDecl enumDecl in enumDecls)
            output.AppendLine(GenerateEnumDeclConstants(enumDecl));

        if (_options.GenerateMacros)
        {
            foreach (VarDecl varDecl in macroVarDecls)
                output.AppendLine(GenerateMacroVarDecl(varDecl));
        }

        if (_options.GenerateExternVariables)
        {
            foreach (VarDecl varDecl in externVarDecls)
                output.AppendLine(GenerateExternVarDeclField(varDecl));

            foreach (VarDecl varDecl in externVarDecls)
                output.AppendLine(GenerateExternVarDeclProperty(varDecl));
        }

        return $$"""
            namespace {{_options.Namespace}}
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1069")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1401")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "IDE0051")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "IDE1006")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SYSLIB1054")]
                public static unsafe partial class {{_options.Class}}
                {
                    public const string DllImportPath = "{{_options.DllImportPath}}";
                    {{(_options.GenerateExternVariables ? $"public static string ExternVariableImportPath {{ get; set; }} = \"{_options.ExternVariableImportPath}\";" : "")}}

                    {{output}}

                    {{(_options.GenerateExternVariables ? Sources.VariableLoader : "")}}
                }
            }
        """;
    }

    private static string GenerateFunctionDecl(FunctionDecl functionDecl)
    {
        IEnumerable<string> parameters = functionDecl.Parameters
            .Select(parameter => $"{GetTypeName(parameter.Type)} {GetValidIdentifier(parameter.Name)}")
            .ToArray();

        return $@"
            [System.Runtime.InteropServices.DllImport(DllImportPath, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            public static extern {GetTypeName(functionDecl.ReturnType)} {GetValidIdentifier(functionDecl.Name)}({string.Join(", ", parameters)});
        ";
    }

    private static string GenerateRecordDecl(RecordDecl recordDecl)
    {
        string recordName = GetCursorName(recordDecl);

        FieldDecl[] fieldsDecls = recordDecl.CursorChildren.OfType<FieldDecl>().ToArray();
        RecordDecl[] recordFieldsDecls = recordDecl.CursorChildren.OfType<RecordDecl>().ToArray();

        StringBuilder fields = new();

        foreach (FieldDecl fieldDecl in fieldsDecls)
            fields.AppendLine(CultureInfo.InvariantCulture, $@"
                {(recordDecl.IsUnion ? "[System.Runtime.InteropServices.FieldOffset(0)]" : "")}
                public {GetTypeName(fieldDecl.Type)} {GetValidIdentifier(fieldDecl.Name)};
            ");

        foreach (RecordDecl recordFieldDecl in recordFieldsDecls)
            fields.AppendLine(GenerateRecordDecl(recordFieldDecl));

        return $@"
            {(recordDecl.IsUnion ? "[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]" : "")}
            public partial struct {recordName}
            {{
                {fields}
            }} 
        ";
    }

    private static string GenerateEnumDecl(EnumDecl enumDecl)
    {
        IEnumerable<string> enums = enumDecl.Decls
            .OfType<EnumConstantDecl>()
            .Select(GenerateEnumConstantDecl);

        return $@"
            public enum {GetCursorName(enumDecl)} : {GetTypeName(enumDecl.IntegerType)}
            {{ 
                {string.Join(",\n", enums)}
            }}
        ";
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
        string enumName = GetCursorName(enumDecl);

        IEnumerable<string> constantFields = enumDecl.Decls.OfType<EnumConstantDecl>().Select(enumConstantDecl =>
        {
            string enumMemberName = GetValidIdentifier(enumConstantDecl.Name);
            return $"public const {enumName} {enumMemberName} = {enumName}.{enumMemberName};";
        });

        return string.Join("\n", constantFields);
    }

    // TODO: Add binding option to allow for generating refs to extern variables
    private static string GenerateMacroVarDecl(VarDecl varDecl)
    {
        if (!varDecl.Name.StartsWith(MacroPrefix, StringComparison.Ordinal))
            return string.Empty;

        if (!varDecl.HasInit)
            return string.Empty;

        Expr init = varDecl.Init;
        CXEvalResult result = varDecl.Handle.Evaluate;

        string typeName = GetTypeName(varDecl.Type);
        string expression;

        switch (result.Kind)
        {
            case CXEvalResultKind.CXEval_Float:
                expression = init.Type.Kind switch
                {
                    CXTypeKind.CXType_Double =>  result.AsDouble.ToString(CultureInfo.InvariantCulture),
                    CXTypeKind.CXType_Float =>  ((float)result.AsDouble).ToString(CultureInfo.InvariantCulture),
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

        string typeName = GetTypeName(varDecl.Type);
        string validName = GetValidIdentifier(varDecl.Name);
        string fieldName = GenerateExternFieldName(validName);

        // We can't use Unsafe.AsRef<T>(void*) because T can't be a pointer.
        return $$"""
            public static ref {{typeName}} {{validName}}
            {
                [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                get
                {
                    if ({{fieldName}} != null)
                        return ref *({{typeName}}*){{fieldName}};

                    @LoadExternVar(nameof({{fieldName}}), out {{fieldName}});
                    return ref *({{typeName}}*){{fieldName}};
                }
            }
        """;
    }

    // This converts value-like macros to type-inferred variables so we can get access to it's type information.
    // The macro's constants will be generated in GenerateVarDecl().
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

    private static string GetAnonymousName(Cursor cursor, string kind)
    {
        cursor.Location.GetFileLocation(out CXFile file, out uint line, out uint column, out _);
        string fileName = Path.GetFileNameWithoutExtension(file.Name.ToString());
        return $"__Anon_{kind}_{fileName}_L{line}_C{column}";
    }

    private static string GetCursorName(NamedDecl namedDecl)
    {
        string name = namedDecl.Name;

        bool isAnonymous =
            string.IsNullOrWhiteSpace(name) ||
            name.StartsWith("struct (unnamed", StringComparison.Ordinal) ||
            name.StartsWith("union (unnamed", StringComparison.Ordinal) ||
            name.StartsWith("enum (unnamed", StringComparison.Ordinal) ||
            name.StartsWith("(unnamed struct", StringComparison.Ordinal) ||
            name.StartsWith("(unnamed union", StringComparison.Ordinal) ||
            name.StartsWith("(unnamed enum", StringComparison.Ordinal);

        if (!isAnonymous)
            return GetValidIdentifier(name);

        if (namedDecl is TypeDecl typeDecl)
            return GetAnonymousName(typeDecl, typeDecl.TypeForDecl.KindSpelling);

        return GetValidIdentifier(name);
    }

    private static string GetTypeName(Type type)
    {
        if (type is AutoType autoType)
            return GetTypeName(autoType.GetDeducedType);

        if (type is BuiltinType builtinType)
        {
            switch (builtinType.Kind)
            {
                case CXTypeKind.CXType_Bool:
                    return "sbyte";
                case CXTypeKind.CXType_Float:
                    return "float";
                case CXTypeKind.CXType_Double:
                    return "double";
                case CXTypeKind.CXType_LongDouble:
                    return "decimal";
                case CXTypeKind.CXType_Void:
                    return "void";
                case CXTypeKind.CXType_Char16:
                case CXTypeKind.CXType_Char32:
                case CXTypeKind.CXType_Char_S:
                case CXTypeKind.CXType_Char_U:
                case CXTypeKind.CXType_SChar:
                case CXTypeKind.CXType_UChar:
                case CXTypeKind.CXType_WChar:
                    return builtinType.Handle.SizeOf switch
                    {
                        1 => builtinType.Handle.IsSigned ? "sbyte" : "byte",
                        2 => builtinType.Handle.IsSigned ? "short" : "ushort",
                        4 => builtinType.Handle.IsSigned ? "int" : "uint",
                        8 => builtinType.Handle.IsSigned ? "long" : "ulong",
                        _ => $"INVALID_CHAR_{builtinType.Kind}"
                    };
                case CXTypeKind.CXType_Short:
                case CXTypeKind.CXType_Int:
                case CXTypeKind.CXType_Long:
                case CXTypeKind.CXType_LongLong:
                    return builtinType.Handle.SizeOf switch
                    {
                        1 => "sbyte",
                        2 => "short",
                        4 => "int",
                        8 => "long",
                        _ => $"INVALID_SIGNED_INTEGER_{builtinType.Kind}_SIZEOF_{builtinType.Handle.SizeOf}"
                    };
                case CXTypeKind.CXType_UShort:
                case CXTypeKind.CXType_UInt:
                case CXTypeKind.CXType_ULong:
                case CXTypeKind.CXType_ULongLong:
                    return builtinType.Handle.SizeOf switch
                    {
                        1 => "byte",
                        2 => "ushort",
                        4 => "uint",
                        8 => "ulong",
                        _ => $"INVALID_UNSIGNED_INTEGER_{builtinType.Kind}_SIZEOF_{builtinType.Handle.SizeOf}"
                    };
                case CXTypeKind.CXType_Invalid:
                case CXTypeKind.CXType_Unexposed:
                case CXTypeKind.CXType_UInt128:
                case CXTypeKind.CXType_Int128:
                case CXTypeKind.CXType_NullPtr:
                case CXTypeKind.CXType_Overload:
                case CXTypeKind.CXType_Dependent:
                case CXTypeKind.CXType_ObjCId:
                case CXTypeKind.CXType_ObjCClass:
                case CXTypeKind.CXType_ObjCSel:
                case CXTypeKind.CXType_Float128:
                case CXTypeKind.CXType_Half:
                case CXTypeKind.CXType_Float16:
                case CXTypeKind.CXType_ShortAccum:
                case CXTypeKind.CXType_Accum:
                case CXTypeKind.CXType_LongAccum:
                case CXTypeKind.CXType_UShortAccum:
                case CXTypeKind.CXType_UAccum:
                case CXTypeKind.CXType_ULongAccum:
                case CXTypeKind.CXType_BFloat16:
                case CXTypeKind.CXType_Ibm128:
                case CXTypeKind.CXType_Complex:
                case CXTypeKind.CXType_Pointer:
                case CXTypeKind.CXType_BlockPointer:
                case CXTypeKind.CXType_LValueReference:
                case CXTypeKind.CXType_RValueReference:
                case CXTypeKind.CXType_Record:
                case CXTypeKind.CXType_Enum:
                case CXTypeKind.CXType_Typedef:
                case CXTypeKind.CXType_ObjCInterface:
                case CXTypeKind.CXType_ObjCObjectPointer:
                case CXTypeKind.CXType_FunctionNoProto:
                case CXTypeKind.CXType_FunctionProto:
                case CXTypeKind.CXType_ConstantArray:
                case CXTypeKind.CXType_Vector:
                case CXTypeKind.CXType_IncompleteArray:
                case CXTypeKind.CXType_VariableArray:
                case CXTypeKind.CXType_DependentSizedArray:
                case CXTypeKind.CXType_MemberPointer:
                case CXTypeKind.CXType_Auto:
                case CXTypeKind.CXType_Elaborated:
                case CXTypeKind.CXType_Pipe:
                case CXTypeKind.CXType_OCLImage1dRO:
                case CXTypeKind.CXType_OCLImage1dArrayRO:
                case CXTypeKind.CXType_OCLImage1dBufferRO:
                case CXTypeKind.CXType_OCLImage2dRO:
                case CXTypeKind.CXType_OCLImage2dArrayRO:
                case CXTypeKind.CXType_OCLImage2dDepthRO:
                case CXTypeKind.CXType_OCLImage2dArrayDepthRO:
                case CXTypeKind.CXType_OCLImage2dMSAARO:
                case CXTypeKind.CXType_OCLImage2dArrayMSAARO:
                case CXTypeKind.CXType_OCLImage2dMSAADepthRO:
                case CXTypeKind.CXType_OCLImage2dArrayMSAADepthRO:
                case CXTypeKind.CXType_OCLImage3dRO:
                case CXTypeKind.CXType_OCLImage1dWO:
                case CXTypeKind.CXType_OCLImage1dArrayWO:
                case CXTypeKind.CXType_OCLImage1dBufferWO:
                case CXTypeKind.CXType_OCLImage2dWO:
                case CXTypeKind.CXType_OCLImage2dArrayWO:
                case CXTypeKind.CXType_OCLImage2dDepthWO:
                case CXTypeKind.CXType_OCLImage2dArrayDepthWO:
                case CXTypeKind.CXType_OCLImage2dMSAAWO:
                case CXTypeKind.CXType_OCLImage2dArrayMSAAWO:
                case CXTypeKind.CXType_OCLImage2dMSAADepthWO:
                case CXTypeKind.CXType_OCLImage2dArrayMSAADepthWO:
                case CXTypeKind.CXType_OCLImage3dWO:
                case CXTypeKind.CXType_OCLImage1dRW:
                case CXTypeKind.CXType_OCLImage1dArrayRW:
                case CXTypeKind.CXType_OCLImage1dBufferRW:
                case CXTypeKind.CXType_OCLImage2dRW:
                case CXTypeKind.CXType_OCLImage2dArrayRW:
                case CXTypeKind.CXType_OCLImage2dDepthRW:
                case CXTypeKind.CXType_OCLImage2dArrayDepthRW:
                case CXTypeKind.CXType_OCLImage2dMSAARW:
                case CXTypeKind.CXType_OCLImage2dArrayMSAARW:
                case CXTypeKind.CXType_OCLImage2dMSAADepthRW:
                case CXTypeKind.CXType_OCLImage2dArrayMSAADepthRW:
                case CXTypeKind.CXType_OCLImage3dRW:
                case CXTypeKind.CXType_OCLSampler:
                case CXTypeKind.CXType_OCLEvent:
                case CXTypeKind.CXType_OCLQueue:
                case CXTypeKind.CXType_OCLReserveID:
                case CXTypeKind.CXType_ObjCObject:
                case CXTypeKind.CXType_ObjCTypeParam:
                case CXTypeKind.CXType_Attributed:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCMcePayload:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImePayload:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCRefPayload:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCSicPayload:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCMceResult:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImeResult:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCRefResult:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCSicResult:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImeResultSingleRefStreamout:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImeResultDualRefStreamout:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImeSingleRefStreamin:
                case CXTypeKind.CXType_OCLIntelSubgroupAVCImeDualRefStreamin:
                case CXTypeKind.CXType_ExtVector:
                case CXTypeKind.CXType_Atomic:
                case CXTypeKind.CXType_BTFTagAttributed:
                default:
                    return $"INVALID_BUILTIN_{builtinType.Kind}";
            }
        }

        if (type is ConstantArrayType constantArrayType)
            return GetTypeName(constantArrayType.ElementType) + "*";

        if (type is ElaboratedType elaboratedType)
            return GetTypeName(elaboratedType.CanonicalType);

        if (type is EnumType enumType)
            return enumType.Decl.Name;

        if (type is FunctionProtoType functionProtoType)
        {
            if (!_options.GenerateFunctionPointers)
                return "System.IntPtr";

            List<string> parameters = functionProtoType.ParamTypes.Select(GetTypeName).ToList();
            parameters.Add(GetTypeName(functionProtoType.ReturnType));
            return $"delegate* unmanaged<{string.Join(", ", parameters)}>";
        }

        if (type is IncompleteArrayType incompleteArrayType)
            return GetTypeName(incompleteArrayType.ElementType) + "*";

        if (type is PointerType pointerType)
        {
            if (pointerType.PointeeType is FunctionProtoType)
                return GetTypeName(pointerType.PointeeType);

            return GetTypeName(pointerType.PointeeType) + "*";
        }

        if (type is RecordType recordType)
        {
            return recordType.Decl.Name switch
            {
                "__va_list_tag" => "void",
                _ => GetCursorName(recordType.Decl)
            };
        }

        return "UNHANDLED_TYPE";
    }

    private static string GetValidIdentifier(string identifier)
    {
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
}
