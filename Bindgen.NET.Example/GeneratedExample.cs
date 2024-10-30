#nullable enable
#pragma warning disable CA1069
namespace ExampleNamespace
{
    public static unsafe partial class ExampleClass
    {
        [System.Runtime.InteropServices.DllImport(BindgenInternal.DllImportPath, EntryPoint = "example_function", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern byte example_function(example_struct_t example_parameter);

        public partial struct example_struct_t
        {
            public uint integer;

            public fixed uint array[4];
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

        public partial class BindgenInternal
        {
            public const string DllImportPath = @"libexample";
        }
    }
}
#pragma warning restore CA1069
#nullable disable
