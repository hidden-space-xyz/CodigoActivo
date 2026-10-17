namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies the supported email guard alert values.
/// </summary>
public enum EmailGuardAlert
{
    /// <summary>
    /// No option is selected.
    /// </summary>
    None,

    /// <summary>
    /// Selects the global budget exhausted option.
    /// </summary>
    GlobalBudgetExhausted,

    /// <summary>
    /// Selects the tracking saturated option.
    /// </summary>
    TrackingSaturated,
}
