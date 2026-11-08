namespace CodigoActivo.API.Security;

/// <summary>
/// Marks an endpoint whose use case hashes or verifies a password with the memory-hard Argon2id,
/// so it waits for a permit of the process-wide pool that bounds that work. Waiting requests hold
/// no Argon2 memory.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class PasswordHashingAttribute : Attribute;
