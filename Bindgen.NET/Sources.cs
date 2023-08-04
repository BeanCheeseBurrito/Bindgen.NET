namespace Bindgen.NET;

public static class Sources
{
    // @ sign is prefixed in case a source header has a declaration with same name
    public const string VariableLoader = """
        private static void @LoadExternVar(string variableSymbol, out void* field)
        {
            if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(ExternVariableImportPath, out System.IntPtr libraryHandle))
                throw new System.NullReferenceException($"Failed to load \"{ExternVariableImportPath}\" native library.");

            if (!System.Runtime.InteropServices.NativeLibrary.TryGetExport(libraryHandle, variableSymbol, out System.IntPtr variableHandle))
                throw new System.NullReferenceException($"Failed to load \"{variableSymbol}\" variable symbol.");

            field = (void*)variableHandle;

            System.Runtime.InteropServices.NativeLibrary.Free(libraryHandle);
        }
    """;
}
