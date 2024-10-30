using System.IO;
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
""";

BindingOptions exampleConfig = new()
{
    Namespace = "ExampleNamespace",
    Class = "ExampleClass",

    DllImportPath = "libexample",

    TreatInputFileAsRawSourceCode = true,
    InputFile = exampleSource,

    // Passing in the include headers provided by zig. See Bindgen.NET.Example.csproj on how to fetch zig's headers.
    SystemIncludeDirectories = { Path.Combine(BuildConstants.ZigLibPath, "include") },

    SuppressedWarnings = { "CA1069" },

    GenerateFunctionPointers = true,
    GenerateMacros = true
};

string generatedSource = BindingGenerator.Generate(exampleConfig);

System.Console.WriteLine(generatedSource);

// Resulting bindings can be viewed in GeneratedExample.cs
// https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/GeneratedExample.cs
