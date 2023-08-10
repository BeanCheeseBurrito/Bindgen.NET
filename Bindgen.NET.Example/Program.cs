using Bindgen.NET;

const string exampleSource = """
// Bindgen.NET has clang headers built-in
#include <stdint.h>
#include <stdbool.h>

// Structs
typedef struct {
    uint32_t integer;
    uint32_t array[4];
} example_struct_t;

// Enums
typedef enum {
    red,
    green,
    blue
} example_enum_t;

// Functions
bool example_function(example_struct_t example_parameter);

// Value-like macros
#define five (5)
#define ten (5 + five)
#define world "World"
#define hello_world "Hello " world

// Extern variables
extern example_struct_t extern_struct;
extern void* extern_pointer;
""";

BindingOptions exampleConfig = new()
{
    Namespace = "ExampleNamespace",
    Class = "ExampleClass",

    DllImportPath = "libexample",
    DllFilePaths = { "libexample", "runtimes/linux-x64/native/libexample" },

    IncludeBuiltInClangHeaders = true,

    TreatInputFileAsRawSourceCode = true,
    InputFile = exampleSource,

    SuppressedWarnings = { "CA1069" },

    GenerateToFilesystem = false,
    GenerateFunctionPointers = true,
    GenerateMacros = true,
    GenerateExternVariables = true,
    GenerateSuppressGcTransition = true
};

string generatedSource = BindingGenerator.Generate(exampleConfig);

System.Console.WriteLine(generatedSource);

// Resulting bindings can be viewed in GeneratedExample.cs
// https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/GeneratedExample.cs
