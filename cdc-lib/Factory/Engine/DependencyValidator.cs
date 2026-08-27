using System;
using System.Collections.Generic;
using System.Linq;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Engine;

/// <summary>
/// Validates dependencies among script groups:
/// - No circular dependencies (DAG check)
/// - All dependency IDs reference existing groups
/// - Dependencies are in lower-or-equal layers (a group in layer 2 can depend on layer 1, not vice versa)
/// </summary>
public static class DependencyValidator
{
    /// <summary>
    /// Validates the given set of script groups.
    /// </summary>
    public static ValidationResult Validate(IReadOnlyList<ScriptGroup> groups)
    {
        var errors = new List<string>();

        var byId = groups.ToDictionary(g => g.Id);

        // 1. Check all dependency IDs exist
        foreach (var group in groups)
        {
            foreach (var depId in group.Dependencies)
            {
                if (!byId.ContainsKey(depId))
                {
                    errors.Add(
                        $"Group '{group.Name}' (Id={group.Id}) depends on unknown group (Id={depId})");
                }
            }
        }

        // 2. Check layer ordering: dependency must be in a lower-or-equal layer
        foreach (var group in groups)
        {
            foreach (var depId in group.Dependencies)
            {
                if (byId.TryGetValue(depId, out var dep))
                {
                    if (dep.Layer > group.Layer)
                    {
                        errors.Add(
                            $"Group '{group.Name}' (Layer={group.Layer}) depends on " +
                            $"'{dep.Name}' (Layer={dep.Layer}) which is in a higher layer. " +
                            "Dependencies must be in lower-or-equal layers.");
                    }
                }
            }
        }

        // 3. Detect circular dependencies (DFS)
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        var cycleErrors = new List<string>();

        foreach (var group in groups)
        {
            if (!visited.Contains(group.Id))
            {
                DetectCycle(group, byId, visiting, visited, cycleErrors, new List<string>());
            }
        }

        errors.AddRange(cycleErrors);

        if (errors.Count > 0)
            return ValidationResult.Fail(errors.ToArray());

        return ValidationResult.Ok();
    }

    /// <summary>
    /// DFS-based circular dependency detection.
    /// Exposed internally for testing.
    /// </summary>
    internal static void DetectCycle(
        ScriptGroup current,
        Dictionary<Guid, ScriptGroup> byId,
        HashSet<Guid> visiting,
        HashSet<Guid> visited,
        List<string> errors,
        List<string> path)
    {
        if (visiting.Contains(current.Id))
        {
            var cyclePath = string.Join(" → ", path.Concat(new[] { current.Name }));
            errors.Add($"Circular dependency detected: {cyclePath}");
            return;
        }

        if (visited.Contains(current.Id))
            return;

        visiting.Add(current.Id);
        path.Add(current.Name);

        foreach (var depId in current.Dependencies)
        {
            if (byId.TryGetValue(depId, out var dep))
            {
                DetectCycle(dep, byId, visiting, visited, errors, path);
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(current.Id);
        visited.Add(current.Id);
    }
}
