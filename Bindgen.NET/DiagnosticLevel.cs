namespace Bindgen.NET;

/// <summary>
/// Specifies the diagnostic level of messages to output.
/// </summary>
public enum DiagnosticLevel
{
    /// <summary>
    /// Output no messages.
    /// </summary>
    Off,
    /// <summary>
    /// Output error messages.
    /// </summary>
    Error,
    /// <summary>
    /// Output warnings and error messages.
    /// </summary>
    Warning,
    /// <summary>
    /// Output info, warnings, and error messages.
    /// </summary>
    Info,
    /// <summary>
    /// Output all messages.
    /// </summary>
    Verbose
}
