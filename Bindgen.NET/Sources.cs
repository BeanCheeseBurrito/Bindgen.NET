namespace Bindgen.NET;

internal static class Sources
{
    public const string Internal = """
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SYSLIB1054")]
        public partial class BindgenInternal
        {
            public static readonly System.Collections.Generic.List<string> DllFilePaths;

            public static System.IntPtr _libraryHandle = System.IntPtr.Zero;

            public static bool IsLinux => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);

            public static bool IsOsx => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

            public static bool IsWindows => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlopen")]
            public static extern System.IntPtr LoadLibraryLinux(string? path, int flags);

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlopen")]
            public static extern System.IntPtr LoadLibraryOsx(string? path, int flags);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "LoadLibrary")]
            public static extern System.IntPtr LoadLibraryWindows(string path);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetModuleHandle")]
            public static extern System.IntPtr GetModuleHandle(string? name);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlsym")]
            public static extern System.IntPtr GetExportLinux(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlsym")]
            public static extern System.IntPtr GetExportOsx(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetProcAddress")]
            public static extern System.IntPtr GetExportWindows(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlerror")]
            public static extern byte* GetLastErrorLinux();

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlerror")]
            public static extern byte* GetLastErrorOsx();

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetLastError")]
            public static extern int GetLastErrorWindows();

            public static bool TryLoad(string path, out System.IntPtr handle)
            {
        #if NET5_0_OR_GREATER
                return System.Runtime.InteropServices.NativeLibrary.TryLoad(path, out handle);
        #else
                handle = System.IntPtr.Zero;

                if (IsLinux)
                    handle = LoadLibraryLinux(path, 0x101);
                else if (IsOsx)
                    handle = LoadLibraryOsx(path, 0x101);
                else if (IsWindows)
                    handle = LoadLibraryWindows(path);

                return handle != System.IntPtr.Zero;
        #endif
            }

            public static System.IntPtr GetExport(string symbol)
            {
        #if NET5_0_OR_GREATER
                return System.Runtime.InteropServices.NativeLibrary.GetExport(_libraryHandle, symbol);
        #else
                if (IsLinux)
                {
                    GetLastErrorLinux();
                    System.IntPtr handle = GetExportLinux(_libraryHandle, symbol);

                    if (handle != System.IntPtr.Zero)
                        return handle;

                    byte* errorResult = GetLastErrorLinux();

                    if (errorResult == null)
                        return handle;

                    string errorMessage = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((System.IntPtr)errorResult)!;
                    throw new System.EntryPointNotFoundException(errorMessage);
                }

                if (IsOsx)
                {
                    GetLastErrorOsx();
                    System.IntPtr handle = GetExportOsx(_libraryHandle, symbol);

                    if (handle != System.IntPtr.Zero)
                        return handle;

                    byte* errorResult = GetLastErrorOsx();

                    if (errorResult == null)
                        return handle;

                    string errorMessage = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((System.IntPtr)errorResult)!;
                    throw new System.EntryPointNotFoundException(errorMessage);
                }

                if (IsWindows)
                {
                    System.IntPtr handle = GetExportWindows(_libraryHandle, symbol);

                    if (handle != System.IntPtr.Zero)
                        return handle;

                    int errorCode = GetLastErrorWindows();
                    string errorMessage = new System.ComponentModel.Win32Exception(errorCode).Message;
                    throw new System.EntryPointNotFoundException($"{errorMessage} \"{symbol}\" not found.");
                }

                throw new System.InvalidOperationException($"Failed to export symbol \"{symbol}\" from dll. Platform is not linux, mac, or windows.");
        #endif
            }

            public static void ResolveLibrary()
            {
                string fileExtension;

                if (IsLinux)
                    fileExtension = ".so";
                else if (IsOsx)
                    fileExtension = ".dylib";
                else if (IsWindows)
                    fileExtension = ".dll";
                else
                    throw new System.InvalidOperationException("Can't determine native library file extension for the current system.");

                foreach (string dllFilePath in DllFilePaths)
                {
                    string fileName = System.IO.Path.GetFileName(dllFilePath);
                    string parentDir = $"{dllFilePath}/..";
                    string searchDir = System.IO.Path.IsPathRooted(dllFilePath)
                        ? System.IO.Path.GetFullPath(parentDir) + "/"
                        : System.IO.Path.GetFullPath(System.AppDomain.CurrentDomain.BaseDirectory + parentDir) + "/";

                    if (TryLoad($"{searchDir}{fileName}", out _libraryHandle))
                        return;

                    if (TryLoad($"{searchDir}{fileName}{fileExtension}", out _libraryHandle))
                        return;

                    if (TryLoad($"{searchDir}lib{fileName}", out _libraryHandle))
                        return;

                    if (TryLoad($"{searchDir}lib{fileName}{fileExtension}", out _libraryHandle))
                        return;

                    if (!fileName.StartsWith("lib") || fileName == "lib")
                        continue;

                    string unprefixed = fileName.Substring(4);

                    if (TryLoad($"{searchDir}{unprefixed}", out _libraryHandle))
                        return;

                    if (TryLoad($"{searchDir}{unprefixed}{fileExtension}", out _libraryHandle))
                        return;
                }

        #if NET7_0_OR_GREATER
                    _libraryHandle = System.Runtime.InteropServices.NativeLibrary.GetMainProgramHandle();
        #else
                if (IsLinux)
                    _libraryHandle = LoadLibraryLinux(null, 0x101);
                else if (IsOsx)
                    _libraryHandle = LoadLibraryOsx(null, 0x101);
                else if (IsWindows)
                    _libraryHandle = GetModuleHandle(null);
        #endif
            }

            public static void* LoadDllSymbol(string variableSymbol, out void* field)
            {
                if (_libraryHandle == System.IntPtr.Zero)
                    ResolveLibrary();

                return field = (void*)GetExport(variableSymbol);
            }
        }
    """;
}
