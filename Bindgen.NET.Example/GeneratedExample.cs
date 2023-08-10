#pragma warning disable CA1069
namespace ExampleNamespace
{
    public static unsafe partial class ExampleClass
    {
        [System.Runtime.InteropServices.SuppressGCTransition]
        [System.Runtime.InteropServices.DllImport(BindgenInternal.DllImportPath, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern byte example_function(example_struct_t example_parameter);

        public partial struct example_struct_t
        {
            public uint integer;

            public array_FixedBuffer array;

            public partial struct array_FixedBuffer
            {
                public uint Item0;

                public uint Item1;

                public uint Item2;

                public uint Item3;
            }
        }

        public enum example_enum_t : uint
        {
            red = 0,
            green = 1,
            blue = 2
        }

        public const example_enum_t red = example_enum_t.red;

        public const example_enum_t green = example_enum_t.green;

        public const example_enum_t blue = example_enum_t.blue;

        public const int five = 5;

        public const string hello_world = "Hello World";

        public const int ten = 10;

        public const string world = "World";

        private static void* extern_pointer_Ptr;

        private static void* extern_struct_Ptr;

        public static ref void* extern_pointer
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (extern_pointer_Ptr != null)
                    return ref *(void**)extern_pointer_Ptr;
                BindgenInternal.LoadDllSymbol("extern_pointer", out extern_pointer_Ptr);
                return ref *(void**)extern_pointer_Ptr;
            }
        }

        public static ref example_struct_t extern_struct
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (extern_struct_Ptr != null)
                    return ref *(example_struct_t*)extern_struct_Ptr;
                BindgenInternal.LoadDllSymbol("extern_struct", out extern_struct_Ptr);
                return ref *(example_struct_t*)extern_struct_Ptr;
            }
        }

        private partial class BindgenInternal
        {
            public const string DllImportPath = "libexample";

            static BindgenInternal()
            {
                DllFilePaths = new string[]
                {
                    "libexample",
                    "runtimes/linux-x64/libexample"
                };
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SYSLIB1054")]
        private partial class BindgenInternal
        {
            private static readonly string[] DllFilePaths;

            private static System.IntPtr _libraryHandle = System.IntPtr.Zero;

            private static bool IsLinux => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);

            private static bool IsOsx => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

            private static bool IsWindows => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlopen")]
            private static extern System.IntPtr LoadLibraryLinux(string? path, int flags);

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlopen")]
            private static extern System.IntPtr LoadLibraryOsx(string? path, int flags);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "LoadLibrary")]
            private static extern System.IntPtr LoadLibraryWindows(string path);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetModuleHandle")]
            private static extern System.IntPtr GetModuleHandle(string? name);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlsym")]
            private static extern System.IntPtr GetExportLinux(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlsym")]
            private static extern System.IntPtr GetExportOsx(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetProcAddress")]
            private static extern System.IntPtr GetExportWindows(System.IntPtr handle, string name);

            [System.Runtime.InteropServices.DllImport("libc", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlerror")]
            private static extern byte* GetLastErrorLinux();

            [System.Runtime.InteropServices.DllImport("libdl", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "dlerror")]
            private static extern byte* GetLastErrorOsx();

            [System.Runtime.InteropServices.DllImport("kernel32", CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = System.Runtime.InteropServices.CharSet.Ansi, EntryPoint = "GetLastError")]
            private static extern int GetLastErrorWindows();

            private static bool TryLoad(string path, out System.IntPtr handle)
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

            private static System.IntPtr GetExport(string symbol)
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

            private static void ResolveLibrary()
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
                    string searchDir = System.IO.Path.IsPathRooted(dllFilePath) ? System.IO.Path.GetFullPath(System.IO.Path.Combine(dllFilePath, "..")) + System.IO.Path.DirectorySeparatorChar : System.AppDomain.CurrentDomain.BaseDirectory;
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

            public static void LoadDllSymbol(string variableSymbol, out void* field)
            {
                if (_libraryHandle == System.IntPtr.Zero)
                    ResolveLibrary();
                field = (void*)GetExport(variableSymbol);
            }
        }
    }
}
#pragma warning restore CA1069