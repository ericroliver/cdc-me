using System;

namespace Softbase.Cdc.Factory.Engine;

/// <summary>
/// Thrown when attempting to delete an entity (connection, template, etc.)
/// that is still referenced by one or more factory orders via a foreign key.
/// The caller should return a 409 Conflict with the message.
/// </summary>
public class ReferencedByOrdersException : Exception
{
    /// <summary>
    /// The type of entity that was being deleted (e.g., "connection", "template").
    /// </summary>
    public string EntityType { get; }

    public ReferencedByOrdersException(string entityType, string message)
        : base(message)
    {
        EntityType = entityType;
    }

    public ReferencedByOrdersException(string entityType, string message, Exception innerException)
        : base(message, innerException)
    {
        EntityType = entityType;
    }
}
