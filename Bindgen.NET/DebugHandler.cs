using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Bindgen.NET;

[SuppressMessage("Usage", "CA2255")]
internal static class DebugHandler
{
    [ModuleInitializer]
    public static void DisableCrashRecovery()
    {
        // Crash recovery interferes with the debugger on linux. See issue here https://github.com/dotnet/ClangSharp/issues/167#issuecomment-818073278
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            SetEnv("LIBCLANG_DISABLE_CRASH_RECOVERY", "1", 1);

        [DllImport("libc", EntryPoint = "setenv", CharSet = CharSet.Ansi)]
        static extern void SetEnv(string name, string value, int overwrite);
    }
}
