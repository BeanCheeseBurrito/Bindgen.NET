# Bindgen.NET
**WORK IN PROGRESS**

## Usage
Download the [nuget package](https://www.nuget.org/packages/Bindgen.NET).
```bash
dotnet add package Bindgen.NET --version 0.0.8
```

A runtime id is needed to resolve the ClangSharp native dependencies. Your project file should like like this.
```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net7.0</TargetFramework>
        <!-- This line is required -->
        <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Bindgen.NET" Version="0.0.8" />
    </ItemGroup>

</Project>
```

Configure your [binding options](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET/BindingOptions.cs) and generate!

Example:
```csharp
using Bindgen.NET;

BindingOptions exampleConfig = new()
{
    Namespace = "ExampleNamespace",
    Class = "ExampleClass",

    DllImportPath = "libexample",
    
    // Some options require manually exporting symbols
    // It will try different name combinations like DllImportAttribute
    DllFilePaths = { 
        "libexample", 
        "example.so", 
        // List your nuget native folders too
        "runtimes/linux-x64/native/example",
        "runtimes/osx-x64/native/libexample",
        "runtimes/win-x64/native/libexample.dll" 
    },

    // Pass raw source code instead
    // TreatInputFileAsRawSourceCode = true,
    InputFile = "path/header.h",
    OutputFile = "path/Header.cs",
    
    // Optional included built-in clang headers
    IncludeBuiltInClangHeaders = true,
    IncludeDirectories = { "path/include" },
    SystemIncludeDirectories = { "path/include" },

    GenerateFunctionPointers = true,
    GenerateMacros = true,
    GenerateExternVariables = true,
    GenerateSuppressGcTransition = true
};

string output = BindingGenerator.Generate(exampleConfig);
```

A runnable example can be found [here](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/Program.cs).

An example of generated bindings can be found in [here](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/GeneratedExample.cs).
