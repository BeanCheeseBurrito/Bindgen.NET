using Bindgen.NET;

const string exampleSource = """
// Structs
typedef struct {
    int field1;
    const char *field2;
} ExampleStruct;

// Enums
typedef enum {
    Red,
    Green,
    Blue
} ExampleEnum;

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

Console.WriteLine(generatedSource);
