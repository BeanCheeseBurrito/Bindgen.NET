# Bindgen.NET
**WORK IN PROGRESS**

## Usage
Download the [nuget package](https://www.nuget.org/packages/Bindgen.NET).
```bash
dotnet add package Bindgen.NET --version 0.0.2
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
        <PackageReference Include="Bindgen.NET" Version="0.0.3" />
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

    DllImportPath = "path/libexample.so",
    ExternVariableImportPath = "path/libexample.so",

    InputFile = "path/header.h",
    OutputFile = "path/Header.cs",
    
    IncludeBuiltInClangHeaders = true,
    IncludeDirectories = { "path/include" },
    SystemIncludeDirectories = { "path/include" },

    GenerateFunctionPointers = true,
    GenerateMacros = true,
    GenerateExternVariables = true
};

BindingGenerator.Generate(exampleConfig);
```

A runnable example and output bindings can be found [here](https://github.com/BeanCheeseBurrito/Bindgen.NET/blob/main/Bindgen.NET.Example/Program.cs).
