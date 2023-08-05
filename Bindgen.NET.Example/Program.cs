using Bindgen.NET;

const string exampleSource = """
// Structs
typedef struct {
    int field1;
    int array[4];
} ExampleStruct;

// Enums
typedef enum {
    Red,
    Green,
    Blue
} ExampleEnum;

// Functions
ExampleStruct ExampleFunction(ExampleEnum ExampleParameter);

// Value-like macros
#define Five (5)
#define Ten (5 + Five)
#define World "World"
#define HelloWorld "Hello " World

// Extern variables
extern ExampleStruct ExternExampleStruct;
extern void* ExternPointer;
""";

BindingOptions exampleConfig = new()
{
    Namespace = "ExampleNamespace",
    Class = "ExampleClass",

    DllImportPath = "libexample",
    ExternVariableImportPath = "libexample",

    TreatInputFileAsRawSourceCode = true,
    InputFile = exampleSource,

    GenerateToFilesystem = false,
    GenerateFunctionPointers = true,
    GenerateMacros = true,
    GenerateExternVariables = true
};

string generatedSource = BindingGenerator.Generate(exampleConfig);

System.Console.WriteLine(generatedSource);

// Below is the resulting bindings
namespace ExampleNamespace
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1069")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1401")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "IDE0051")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "IDE1006")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SYSLIB1054")]
    public static unsafe partial class ExampleClass
    {
        public const string DllImportPath = "libexample";

        public static string ExternVariableImportPath { get; set; } = "libexample";

        [System.Runtime.InteropServices.DllImport(DllImportPath, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern ExampleStruct ExampleFunction(ExampleEnum ExampleParameter);

        public partial struct ExampleStruct
        {
            public int field1;

            public array_Fixed_Buffer array;

            public partial struct array_Fixed_Buffer
            {
                public int Item0;

                public int Item1;

                public int Item2;

                public int Item3;
            }
        }

        public enum ExampleEnum : uint
        {
            Red = 0,
            Green = 1,
            Blue = 2
        }

        public const ExampleEnum Red = ExampleEnum.Red;

        public const ExampleEnum Green = ExampleEnum.Green;

        public const ExampleEnum Blue = ExampleEnum.Blue;

        public const int Five = 5;

        public const string HelloWorld = "Hello World";

        public const int Ten = 10;

        public const string World = "World";

        private static void* ExternExampleStruct_Ptr;

        private static void* ExternPointer_Ptr;

        public static ref ExampleStruct ExternExampleStruct
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (ExternExampleStruct_Ptr != null)
                    return ref *(ExampleStruct*)ExternExampleStruct_Ptr;
                @LoadExternVar("ExternExampleStruct", out ExternExampleStruct_Ptr);
                return ref *(ExampleStruct*)ExternExampleStruct_Ptr;
            }
        }

        public static ref void* ExternPointer
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (ExternPointer_Ptr != null)
                    return ref *(void**)ExternPointer_Ptr;
                @LoadExternVar("ExternPointer", out ExternPointer_Ptr);
                return ref *(void**)ExternPointer_Ptr;
            }
        }

        private static void @LoadExternVar(string variableSymbol, out void* field)
        {
            if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(ExternVariableImportPath, out System.IntPtr libraryHandle))
                throw new System.NullReferenceException($"Failed to load \"{ExternVariableImportPath}\" native library.");
            if (!System.Runtime.InteropServices.NativeLibrary.TryGetExport(libraryHandle, variableSymbol, out System.IntPtr variableHandle))
                throw new System.NullReferenceException($"Failed to load \"{variableSymbol}\" variable symbol.");
            field = (void*)variableHandle;
            System.Runtime.InteropServices.NativeLibrary.Free(libraryHandle);
        }
    }
}
