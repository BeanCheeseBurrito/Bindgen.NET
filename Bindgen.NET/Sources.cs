namespace Bindgen.NET;

internal static class Sources
{
    public const string LibraryLoader = """
        private static void BindgenLoadLibrary()
        {
            string fileExtension;

            if (System.OperatingSystem.IsWindows())
                fileExtension = ".dll";
            else if (System.OperatingSystem.IsMacOS())
                fileExtension = ".dylib";
            else if (System.OperatingSystem.IsLinux())
                fileExtension = ".so";
            else
                throw new System.InvalidOperationException("Can't determine file extension for system");

            string fullPath = System.IO.Path.GetFullPath(ExternVariableImportPath);
            string fileName = System.IO.Path.GetFileName(fullPath);
            string parentDir = fullPath.Substring(0, fullPath.Length - fileName.Length);

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}{fileName}", out BindgenLibraryHandle))
                return;

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}{fileName}{fileExtension}", out BindgenLibraryHandle))
                return;

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}lib{fileName}", out BindgenLibraryHandle))
                return;

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}lib{fileName}{fileExtension}", out BindgenLibraryHandle))
                return;

            if (!fileName.StartsWith("lib"))
                throw new System.DllNotFoundException("Unable to load library path");

            string unprefixed = fileName.Substring(4);

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}{unprefixed}", out BindgenLibraryHandle))
                return;

            if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{parentDir}{unprefixed}{fileExtension}", out @BindgenLibraryHandle))
                return;

            throw new System.DllNotFoundException("Unable to load library path");
        }
    """;

    public const string VariableLoader = """
        private static void BindgenLoadExternVar(string variableSymbol, out void* field)
        {
            if (BindgenLibraryHandle == System.IntPtr.Zero)
                BindgenLoadLibrary();

            if (!System.Runtime.InteropServices.NativeLibrary.TryGetExport(BindgenLibraryHandle, variableSymbol, out System.IntPtr variableHandle))
                throw new System.NullReferenceException($"Failed to load \"{variableSymbol}\" variable symbol.");

            field = (void*)variableHandle;
        }
    """;
}
