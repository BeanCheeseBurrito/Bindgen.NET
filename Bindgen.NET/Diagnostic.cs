namespace Bindgen.NET;

internal static class Diagnostic
{
    public static DiagnosticLevel CurrentDiagnosticLevel { get; set; } = DiagnosticLevel.Info;

    public static void Log(DiagnosticLevel diagnosticLevel, string message)
    {
        if (!(diagnosticLevel <= CurrentDiagnosticLevel))
            return;

        Console.WriteLine($"{diagnosticLevel}: {message}");
    }
}
