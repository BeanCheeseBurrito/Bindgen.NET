# Bindgen.NET
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=for-the-badge)](https://github.com/SanderMertens/flecs/blob/master/LICENSE)
[![Nuget](https://img.shields.io/nuget/v/Bindgen.NET?style=for-the-badge)](https://www.nuget.org/packages/Bindgen.NET/)

**WORK IN PROGRESS**

## Usage
Download the [nuget package](https://www.nuget.org/packages/Bindgen.NET).
```console
dotnet add package Bindgen.NET --version *-*
```

A runtime id is needed to resolve the ClangSharp native dependencies. Your project file should look like this.
```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net7.0</TargetFramework>
        <!-- This line is required -->
        <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Bindgen.NET" Version="*-*" />
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

    // Pass raw source code instead
    // TreatInputFileAsRawSourceCode = true,
    InputFile = "path/header.h",
    OutputFile = "path/Header.cs",
    
    IncludeDirectories = { "path/include" },
    SystemIncludeDirectories = { "path/include" },

    GenerateFunctionPointers = true,
    GenerateMacros = true,
    GenerateStructEqualityFunctions = true
};

string output = BindingGenerator.Generate(exampleConfig);
```

A runnable example can be found [here](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/Program.cs).

A basic example of generated bindings can be found in [here](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/GeneratedExample.cs).

A real world example of generated bindings can be seen in the [Flecs.NET](https://github.com/BeanCheeseBurrito/Flecs.NET/tree/main) repo [here](https://github.com/BeanCheeseBurrito/Flecs.NET/blob/main/src/Flecs.NET.Bindings/Flecs.g.cs).
