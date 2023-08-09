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
                BindgenInternal.LoadExternVar("extern_pointer", out extern_pointer_Ptr);
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
                BindgenInternal.LoadExternVar("extern_struct", out extern_struct_Ptr);
                return ref *(example_struct_t*)extern_struct_Ptr;
            }
        }

        private static class BindgenInternal
        {
            public const string DllImportPath = "libexample";

            private static readonly string[] DllFilePaths =
            {
                "libexample",
                "runtimes/linux-x64/libexample"
            };

            private static System.IntPtr _libraryHandle = System.IntPtr.Zero;

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
                    string searchDir = System.IO.Path.IsPathRooted(dllFilePath) ? System.IO.Path.GetFullPath(System.IO.Path.Combine(dllFilePath, "..")) + System.IO.Path.DirectorySeparatorChar : System.AppDomain.CurrentDomain.BaseDirectory;
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

            public static void LoadExternVar(string variableSymbol, out void* field)
            {
                if (_libraryHandle == System.IntPtr.Zero)
                    LoadLibrary();
                field = (void*)System.Runtime.InteropServices.NativeLibrary.GetExport(_libraryHandle, variableSymbol);
            }
        }
    }
}
#pragma warning restore CA1069
