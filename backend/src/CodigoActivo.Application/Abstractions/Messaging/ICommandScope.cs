namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Names the command that is running in a scope, so what it changes can be traced back to it. The
/// audit decorator enters it around every command, audited or not; a command run by another one
/// names itself until it ends and then gives the name back.
/// </summary>
public interface ICommandScope
{
    /// <summary>
    /// Gets the name of the running command without its <c>Command</c> suffix, or
    /// <see langword="null"/> when no command is running.
    /// </summary>
    public string? Command { get; }

    /// <summary>
    /// Marks the start of a command.
    /// </summary>
    /// <param name="command">Name of the command without its <c>Command</c> suffix.</param>
    /// <returns>A handle that gives back the previous name when disposed.</returns>
    public IDisposable Enter(string command);
}
