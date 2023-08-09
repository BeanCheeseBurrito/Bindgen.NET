namespace Bindgen.NET;

internal static class Sources
{
    public const string LibraryLoader = """
        private static void LoadLibrary()
        {
            string fileExtension;

            if (System.OperatingSystem.IsWindows())
                fileExtension = ".dll";
            else if (System.OperatingSystem.IsMacOS())
                fileExtension = ".dylib";
            else if (System.OperatingSystem.IsLinux())
                fileExtension = ".so";
            else
                throw new System.InvalidOperationException("Can't determine native library file extension for the current system.");

            foreach (string dllFilePath in DllFilePaths)
            {
                string fileName = System.IO.Path.GetFileName(dllFilePath);

                string searchDir = System.IO.Path.IsPathRooted(dllFilePath)
                    ? System.IO.Path.GetFullPath(System.IO.Path.Combine(dllFilePath, "..")) + System.IO.Path.DirectorySeparatorChar
                    : System.AppDomain.CurrentDomain.BaseDirectory;

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}{fileName}", out _libraryHandle))
                    return;

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}{fileName}{fileExtension}", out _libraryHandle))
                    return;

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}lib{fileName}", out _libraryHandle))
                    return;

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}lib{fileName}{fileExtension}", out _libraryHandle))
                    return;

                if (!fileName.StartsWith("lib") || fileName == "lib")
                    continue;

                string unprefixed = fileName.Substring(4);

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}{unprefixed}", out _libraryHandle))
                    return;

                if (System.Runtime.InteropServices.NativeLibrary.TryLoad($"{searchDir}{unprefixed}{fileExtension}", out _libraryHandle))
                    return;
            }

            _libraryHandle = System.Runtime.InteropServices.NativeLibrary.GetMainProgramHandle();
        }
    """;

    public const string VariableLoader = """
        public static void LoadExternVar(string variableSymbol, out void* field)
        {
            if (_libraryHandle == System.IntPtr.Zero)
                LoadLibrary();

            field = (void*)System.Runtime.InteropServices.NativeLibrary.GetExport(_libraryHandle, variableSymbol);
        }
    """;
}
