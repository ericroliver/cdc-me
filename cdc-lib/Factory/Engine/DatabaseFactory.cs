using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Engine;

/// <summary>
/// Orchestrates the full database provisioning workflow:
/// resolve connection → validate → resolve parameters → restore → hydrate → deliver.
/// </summary>
public class DatabaseFactory : IDatabaseFactory
{
    private readonly IOrderRepository _orderRepository;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IDatabaseTemplateRepository _templateRepository;
    private readonly IScriptGroupRepository _scriptGroupRepository;
    private readonly IScriptLibrary _scriptLibrary;
    private readonly IDatabaseProvider _databaseProvider;
    private readonly IScriptExecutor _scriptExecutor;
    private readonly ParameterResolver _parameterResolver;
    private readonly ILogger<DatabaseFactory> _logger;

    public DatabaseFactory(
        IOrderRepository orderRepository,
        IConnectionRegistry connectionRegistry,
        IDatabaseTemplateRepository templateRepository,
        IScriptGroupRepository scriptGroupRepository,
        IScriptLibrary scriptLibrary,
        IDatabaseProvider databaseProvider,
        IScriptExecutor scriptExecutor,
        ParameterResolver parameterResolver,
        ILogger<DatabaseFactory> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _connectionRegistry = connectionRegistry ?? throw new ArgumentNullException(nameof(connectionRegistry));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _scriptGroupRepository = scriptGroupRepository ?? throw new ArgumentNullException(nameof(scriptGroupRepository));
        _scriptLibrary = scriptLibrary ?? throw new ArgumentNullException(nameof(scriptLibrary));
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
        _parameterResolver = parameterResolver ?? throw new ArgumentNullException(nameof(parameterResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Order> OrderAsync(OrderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.TemplateId == Guid.Empty)
            throw new ArgumentException("TemplateId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TargetDatabaseName))
            throw new ArgumentException("TargetDatabaseName is required.", nameof(request));

        // Step 1: Create the order record (status = Pending)
        var order = await _orderRepository.CreateAsync(request);

        try
        {
            // Step 2: Resolve Connection
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Resolving, startedAt: DateTime.UtcNow);
            var connection = await ResolveConnectionAsync(request.TargetConnectionId, request.TargetConnectionName);
            order.TargetConnectionId = connection.Id;

            // Step 3: Validate
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Validating);
            var template = await ValidateTemplateAsync(request.TemplateId, connection);
            var scriptGroups = await ValidateScriptGroupsAsync(request.ScriptGroupIds);

            // Step 4: Resolve Parameters
            var mergedParameters = _parameterResolver.MergeParameters(
                request.Parameters,
                request.ParameterFilePath);
            var resolvedDatabaseName = _parameterResolver.ResolveDatabaseName(
                request.TargetDatabaseName,
                mergedParameters);

            // Persist order details (parameters + script groups)
            await _orderRepository.PersistOrderDetailsAsync(
                order.Id, request.ScriptGroupIds, mergedParameters);

            // Step 5: Restore
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Restoring);
            var restoreResult = await _databaseProvider.RestoreBackupAsync(
                template.FilePath,
                resolvedDatabaseName,
                connection.ConnectionString);

            if (!restoreResult.Success)
            {
                throw new FactoryException("Restoring",
                    $"Restore failed: {restoreResult.ErrorMessage}");
            }

            // Step 6: Hydrate
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Hydrating);
            await HydrateAsync(scriptGroups, mergedParameters, connection.ConnectionString);

            // Step 7: Deliver
            await _orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Delivered, completedAt: DateTime.UtcNow);
            await _orderRepository.RecordProvisionedDatabaseAsync(
                order.Id, resolvedDatabaseName, connection.Id, template.Id);

            order.Status = nameof(OrderStatus.Delivered);
            order.TargetDatabaseName = resolvedDatabaseName;
            order.CompletedAt = DateTime.UtcNow;
            order.Parameters = mergedParameters;
            order.ScriptGroupIds = request.ScriptGroupIds;

            _logger.LogInformation(
                "Order {OrderId} delivered: database '{DbName}' on connection '{ConnectionName}'",
                order.Id, resolvedDatabaseName, connection.Name);

            return order;
        }
        catch (FactoryException ex)
        {
            _logger.LogError(ex, "Order {OrderId} failed at step {Step}: {Message}",
                order.Id, ex.Step, ex.Message);

            await _orderRepository.FailAsync(order.Id, ex.Message);

            order.Status = nameof(OrderStatus.Failed);
            order.ErrorMessage = ex.Message;
            order.CompletedAt = DateTime.UtcNow;
            order.ScriptGroupIds = request.ScriptGroupIds;
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} failed unexpectedly: {Message}", order.Id, ex.Message);

            await _orderRepository.FailAsync(order.Id, ex.Message);

            order.Status = nameof(OrderStatus.Failed);
            order.ErrorMessage = ex.Message;
            order.CompletedAt = DateTime.UtcNow;
            order.ScriptGroupIds = request.ScriptGroupIds;
            return order;
        }
    }

    private async Task<Connection> ResolveConnectionAsync(Guid? targetConnectionId, string? targetConnectionName)
    {
        // 1. Explicit connection ID takes precedence
        if (targetConnectionId.HasValue && targetConnectionId.Value != Guid.Empty)
        {
            var connection = await _connectionRegistry.GetByIdAsync(targetConnectionId.Value);
            if (connection is null)
            {
                throw new FactoryException("Resolving",
                    $"Connection not found: {targetConnectionId.Value}");
            }

            _logger.LogInformation("Resolved connection '{Name}' (Id={Id}) for order", connection.Name, connection.Id);
            return connection;
        }

        // 2. Fall back to connection name if provided
        if (!string.IsNullOrWhiteSpace(targetConnectionName))
        {
            var connection = await _connectionRegistry.GetByNameAsync(targetConnectionName);
            if (connection is null)
            {
                throw new FactoryException("Resolving",
                    $"Connection not found with name: {targetConnectionName}");
            }

            _logger.LogInformation("Resolved connection '{Name}' (Id={Id}) by name for order", connection.Name, connection.Id);
            return connection;
        }

        // 3. Fall back to default connection
        var defaultConnection = await _connectionRegistry.GetDefaultAsync();
        if (defaultConnection is null)
        {
            throw new FactoryException("Resolving",
                "No target connection specified (by ID or name) and no default connection is set");
        }

        _logger.LogInformation("Resolved default connection '{Name}' for order", defaultConnection.Name);
        return defaultConnection;
    }

    private async Task<Template> ValidateTemplateAsync(Guid templateId, Connection connection)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template is null)
        {
            throw new FactoryException("Validating",
                $"Template not found: {templateId}");
        }

        if (string.IsNullOrWhiteSpace(template.FilePath))
        {
            throw new FactoryException("Validating",
                $"Template '{template.Name}' has no backup file path");
        }

        if (!string.Equals(template.Platform, connection.Platform, StringComparison.OrdinalIgnoreCase))
        {
            throw new FactoryException("Validating",
                $"Platform mismatch: template platform '{template.Platform}' does not match " +
                $"connection platform '{connection.Platform}'");
        }

        _logger.LogInformation(
            "Validated template '{Name}' (Id={Id}) — platform '{Platform}' matches connection",
            template.Name, template.Id, template.Platform);

        return template;
    }

    private async Task<IReadOnlyList<ScriptGroup>> ValidateScriptGroupsAsync(IReadOnlyList<Guid> scriptGroupIds)
    {
        var groupIds = scriptGroupIds ?? Array.Empty<Guid>();
        if (groupIds.Count == 0)
        {
            _logger.LogInformation("No script groups specified — skipping hydration");
            return Array.Empty<ScriptGroup>();
        }

        var groups = new List<ScriptGroup>(groupIds.Count);
        foreach (var groupId in groupIds)
        {
            var group = await _scriptGroupRepository.GetGroupAsync(groupId);
            if (group is null)
            {
                throw new FactoryException("Validating",
                    $"Script group not found: {groupId}");
            }
            groups.Add(group);
        }

        var dependencyResult = DependencyValidator.Validate(groups);
        if (!dependencyResult.IsValid)
        {
            throw new FactoryException("Validating",
                $"Dependency validation failed: {string.Join("; ", dependencyResult.Errors)}");
        }

        return groups;
    }

    private async Task HydrateAsync(
        IReadOnlyList<ScriptGroup> scriptGroups,
        IReadOnlyDictionary<string, object?> parameters,
        string connectionString)
    {
        // Order by layer (ascending), then by order within layer
        var sortedGroups = scriptGroups
            .OrderBy(g => g.Layer)
            .ThenBy(g => g.Order)
            .ToList();

        var completedGroupIds = new HashSet<Guid>();

        foreach (var group in sortedGroups)
        {
            // Check that all dependencies have been completed
            if (group.Dependencies != null)
            {
                foreach (var depId in group.Dependencies)
                {
                    if (!completedGroupIds.Contains(depId))
                    {
                        throw new FactoryException("Hydrating",
                            $"Group '{group.Name}' depends on group '{depId}' " +
                            "which has not been executed yet. " +
                            "This indicates a layer/order/dependency conflict.");
                    }
                }
            }

            // Get scripts for this group, ordered by script order
            var scripts = await _scriptLibrary.ListScriptsAsync(group.Id);
            var sortedScripts = scripts.OrderBy(s => s.Order).ToList();

            _logger.LogInformation(
                "Executing group '{GroupName}' (Layer={Layer}, {ScriptCount} scripts)",
                group.Name, group.Layer, sortedScripts.Count);

            foreach (var script in sortedScripts)
            {
                _logger.LogInformation("Executing script '{ScriptName}'...", script.Name);

                var result = await _scriptExecutor.ExecuteAsync(script, parameters, connectionString);

                if (!result.Success)
                {
                    throw new FactoryException("Hydrating",
                        $"Script '{script.Name}' (group '{group.Name}') failed: {result.ErrorMessage}");
                }

                _logger.LogInformation(
                    "Script '{ScriptName}' completed ({RowsAffected} rows, {ElapsedMs}ms)",
                    script.Name, result.RowsAffected, result.ExecutionTime.TotalMilliseconds);
            }

            completedGroupIds.Add(group.Id);
        }
    }
}
