using System;

namespace Softbase.Cdc.Factory.Engine;

/// <summary>
/// Exception thrown when a step in the factory pipeline fails.
/// Carries the step name for logging and status tracking.
/// </summary>
public class FactoryException : Exception
{
    /// <summary>
    /// The pipeline step that failed (e.g., "Resolving", "Validating").
    /// </summary>
    public string Step { get; }

    public FactoryException(string step, string message) : base(message)
    {
        Step = step;
    }

    public FactoryException(string step, string message, Exception innerException)
        : base(message, innerException)
    {
        Step = step;
    }
}
