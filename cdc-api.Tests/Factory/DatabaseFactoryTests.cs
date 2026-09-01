using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Xunit;
using ParamDict = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace cdc_api.Tests.Factory;

public class DatabaseFactoryTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IConnectionRegistry> _connectionRegistry = new();
    private readonly Mock<IDatabaseTemplateRepository> _templateRepository = new();
    private readonly Mock<IScriptGroupRepository> _scriptGroupRepository = new();
    private readonly Mock<IScriptLibrary> _scriptLibrary = new();
    private readonly Mock<IScriptExecutor> _scriptExecutor = new();
    private readonly Mock<IDatabaseProvider> _databaseProvider = new();
    private readonly ParameterResolver _parameterResolver = new(NullLogger<ParameterResolver>.Instance);
    private readonly DatabaseFactory _factory;

    public DatabaseFactoryTests()
    {
        // Default: CreateAsync returns a valid order
        _orderRepository.Setup(r => r.CreateAsync(It.IsAny<OrderRequest>()))
            .ReturnsAsync((OrderRequest req) => new Order
            {
                Id = Guid.NewGuid(),
                TemplateId = req.TemplateId,
                TargetConnectionId = req.TargetConnectionId,
                TargetDatabaseName = req.TargetDatabaseName,
                Status = nameof(OrderStatus.Pending),
                CreatedAt = DateTime.UtcNow
            });

        _factory = new DatabaseFactory(
            _orderRepository.Object,
            _connectionRegistry.Object,
            _templateRepository.Object,
            _scriptGroupRepository.Object,
            _scriptLibrary.Object,
            _databaseProvider.Object,
            _scriptExecutor.Object,
            _parameterResolver,
            NullLogger<DatabaseFactory>.Instance);
    }

    private static Template MakeTemplate() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Template",
        Version = "1.0",
        Platform = "SqlServer",
        FilePath = "/backups/template.bak",
        CreatedAt = DateTime.UtcNow
    };

    private static Connection MakeConnection(Guid? id = null, bool isDefault = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Connection",
        Platform = "SqlServer",
        ConnectionString = "Server=localhost;Database=master;Integrated Security=true;",
        IsDefault = isDefault,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ScriptGroup MakeGroup(int layer, int order, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = $"Group-L{layer}-O{order}",
        Layer = layer,
        Order = order,
        Dependencies = Array.Empty<Guid>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Script MakeScript(Guid groupId, int order) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Script-{order}",
        ScriptGroupId = groupId,
        Order = order,
        Type = "SqlScript",
        Content = "SELECT 1",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private OrderRequest MakeRequest(
        Guid templateId,
        Guid? connectionId = null,
        string? connectionName = null,
        Guid[]? scriptGroupIds = null,
        Dictionary<string, object?>? parameters = null) => new()
        {
            TemplateId = templateId,
            TargetConnectionId = connectionId,
            TargetConnectionName = connectionName,
            TargetDatabaseName = "acme_test_{date}",
            ScriptGroupIds = scriptGroupIds ?? Array.Empty<Guid>(),
            Parameters = parameters
        };

    // ───────────────────────────────────────────────────────────────
    // Resolve Connection tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_ResolvesDefaultConnection_WhenTargetConnectionIdIsNull()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.TargetConnectionId.Should().Be(defaultConn.Id);
        _connectionRegistry.Verify(r => r.GetDefaultAsync(), Times.Once);
        _connectionRegistry.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task OrderAsync_FailsWhenNoDefaultConnectionAndNoTargetSpecified()
    {
        var template = MakeTemplate();
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync((Connection?)null);

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("no default connection");
    }

    [Fact]
    public async Task OrderAsync_UsesSpecifiedConnection_WhenTargetConnectionIdProvided()
    {
        var template = MakeTemplate();
        var conn = MakeConnection();

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetByIdAsync(conn.Id)).ReturnsAsync(conn);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, connectionId: conn.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.TargetConnectionId.Should().Be(conn.Id);
        _connectionRegistry.Verify(r => r.GetByIdAsync(conn.Id), Times.Once);
        _connectionRegistry.Verify(r => r.GetDefaultAsync(), Times.Never);
    }

    [Fact]
    public async Task OrderAsync_ResolvesConnectionByName_WhenIdNotProvided()
    {
        var template = MakeTemplate();
        var conn = MakeConnection();
        conn.Name = "field-sqlserver";

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetByNameAsync("field-sqlserver")).ReturnsAsync(conn);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, connectionName: "field-sqlserver");

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.TargetConnectionId.Should().Be(conn.Id);
        _connectionRegistry.Verify(r => r.GetByNameAsync("field-sqlserver"), Times.Once);
        _connectionRegistry.Verify(r => r.GetDefaultAsync(), Times.Never);
        _connectionRegistry.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task OrderAsync_FailsWhenConnectionNameNotFound()
    {
        var template = MakeTemplate();

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetByNameAsync("missing")).ReturnsAsync((Connection?)null);

        var request = MakeRequest(template.Id, connectionName: "missing");

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Connection not found with name: missing");
    }

    [Fact]
    public async Task OrderAsync_IdTakesPrecedenceOverName()
    {
        var template = MakeTemplate();
        var connById = MakeConnection();
        connById.Name = "by-id-connection";
        var connByName = MakeConnection();
        connByName.Name = "by-name-connection";

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetByIdAsync(connById.Id)).ReturnsAsync(connById);
        _connectionRegistry.Setup(r => r.GetByNameAsync("by-name-connection")).ReturnsAsync(connByName);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, connectionId: connById.Id, connectionName: "by-name-connection");

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.TargetConnectionId.Should().Be(connById.Id);
        _connectionRegistry.Verify(r => r.GetByIdAsync(connById.Id), Times.Once);
        _connectionRegistry.Verify(r => r.GetByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OrderAsync_FailsWhenNoConnectionIdNameOrDefault()
    {
        var template = MakeTemplate();

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync((Connection?)null);

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("No target connection specified");
        order.ErrorMessage.Should().Contain("no default connection");
    }

    [Fact]
    public async Task OrderAsync_FailsWhenSpecifiedConnectionNotFound()
    {
        var template = MakeTemplate();
        var connId = Guid.NewGuid();

        _connectionRegistry.Setup(r => r.GetByIdAsync(connId)).ReturnsAsync((Connection?)null);

        var request = MakeRequest(template.Id, connectionId: connId);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task OrderAsync_PreservesScriptGroupIdsOnFailure()
    {
        var template = MakeTemplate();
        var conn = MakeConnection();
        var groupIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _connectionRegistry.Setup(r => r.GetByIdAsync(conn.Id)).ReturnsAsync(conn);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Fail("disk full"));

        var request = MakeRequest(template.Id, connectionId: conn.Id, scriptGroupIds: groupIds);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ScriptGroupIds.Should().BeEquivalentTo(groupIds);
    }

    // ───────────────────────────────────────────────────────────────
    // Validation tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_FailsWhenTemplateNotFound()
    {
        var templateId = Guid.NewGuid();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync((Template?)null);

        var request = MakeRequest(templateId);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Template").And.Contain("not found");
    }

    [Fact]
    public async Task OrderAsync_FailsWhenTemplateHasNoFilePath()
    {
        var template = MakeTemplate();
        template.FilePath = "";
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("no backup file path");
    }

    [Fact]
    public async Task OrderAsync_FailsWhenPlatformMismatch()
    {
        var template = MakeTemplate();
        template.Platform = "PostgreSql";
        var defaultConn = MakeConnection(isDefault: true);
        defaultConn.Platform = "SqlServer";

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("platform").And.Contain("does not match");
    }

    [Fact]
    public async Task OrderAsync_FailsWhenScriptGroupNotFound()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);
        var groupId = Guid.NewGuid();

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(groupId)).ReturnsAsync((ScriptGroup?)null);

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { groupId });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Script group").And.Contain("not found");
    }

    // ───────────────────────────────────────────────────────────────
    // Restore tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_FailsWhenRestoreFails()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Fail("Backup file not found"));

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Restore failed");
    }

    [Fact]
    public async Task OrderAsync_CallsRestoreWithCorrectArguments()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(
                template.FilePath, It.IsAny<string>(), defaultConn.ConnectionString))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        await _factory.OrderAsync(request);

        _databaseProvider.Verify(p => p.RestoreBackupAsync(
            template.FilePath,
            It.Is<string>(s => s.Contains("acme_test")),
            defaultConn.ConnectionString), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Hydrate tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_ExecutesGroupsInLayerOrder()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);
        var group1 = MakeGroup(layer: 1, order: 1);
        var group2 = MakeGroup(layer: 2, order: 1);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group1.Id)).ReturnsAsync(group1);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group2.Id)).ReturnsAsync(group2);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var script1a = MakeScript(group1.Id, 1);
        var script2a = MakeScript(group2.Id, 1);

        _scriptLibrary.Setup(s => s.ListScriptsAsync(group1.Id))
            .ReturnsAsync(new List<Script> { script1a });
        _scriptLibrary.Setup(s => s.ListScriptsAsync(group2.Id))
            .ReturnsAsync(new List<Script> { script2a });

        _scriptExecutor.Setup(e => e.ExecuteAsync(It.IsAny<Script>(), It.IsAny<ParamDict>(), It.IsAny<string>()))
            .ReturnsAsync(ScriptResult.Ok("test", 1, TimeSpan.Zero));

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { group1.Id, group2.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));

        var calls = _scriptExecutor.Invocations
            .Where(i => i.Method.Name == nameof(IScriptExecutor.ExecuteAsync))
            .Select(i => (Script)i.Arguments[0])
            .ToList();

        calls[0].Id.Should().Be(script1a.Id);
        calls[1].Id.Should().Be(script2a.Id);
    }

    [Fact]
    public async Task OrderAsync_ExecutesScriptsInOrderWithinGroup()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);
        var group = MakeGroup(layer: 1, order: 1);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group.Id)).ReturnsAsync(group);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var script2 = MakeScript(group.Id, 2);
        var script1 = MakeScript(group.Id, 1);
        _scriptLibrary.Setup(s => s.ListScriptsAsync(group.Id))
            .ReturnsAsync(new List<Script> { script2, script1 });

        _scriptExecutor.Setup(e => e.ExecuteAsync(It.IsAny<Script>(), It.IsAny<ParamDict>(), It.IsAny<string>()))
            .ReturnsAsync(ScriptResult.Ok("test", 1, TimeSpan.Zero));

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { group.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));

        var calls = _scriptExecutor.Invocations
            .Where(i => i.Method.Name == nameof(IScriptExecutor.ExecuteAsync))
            .Select(i => (Script)i.Arguments[0])
            .ToList();

        calls[0].Id.Should().Be(script1.Id);
        calls[1].Id.Should().Be(script2.Id);
    }

    [Fact]
    public async Task OrderAsync_FailsWhenScriptExecutionFails()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);
        var group = MakeGroup(layer: 1, order: 1);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group.Id)).ReturnsAsync(group);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var script = MakeScript(group.Id, 1);
        _scriptLibrary.Setup(s => s.ListScriptsAsync(group.Id))
            .ReturnsAsync(new List<Script> { script });

        _scriptExecutor.Setup(e => e.ExecuteAsync(It.IsAny<Script>(), It.IsAny<ParamDict>(), It.IsAny<string>()))
            .ReturnsAsync(ScriptResult.Fail("bad-script", "Syntax error", TimeSpan.Zero));

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { group.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Syntax error");
    }

    [Fact]
    public async Task OrderAsync_SucceedsWithNoScriptGroups()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, scriptGroupIds: Array.Empty<Guid>());

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        _scriptExecutor.Verify(e => e.ExecuteAsync(It.IsAny<Script>(), It.IsAny<ParamDict>(), It.IsAny<string>()), Times.Never);
    }

    // ───────────────────────────────────────────────────────────────
    // Parameter resolution tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_ResolvesDatabaseNameTemplate()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, parameters: new Dictionary<string, object?>
        {
            ["CompanyName"] = "Acme"
        });
        request.TargetDatabaseName = "db_{CompanyName}_{date}";

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.TargetDatabaseName.Should().StartWith("db_Acme_");
        order.TargetDatabaseName.Should().NotContain("{CompanyName}");
        order.TargetDatabaseName.Should().NotContain("{date}");
    }

    // ───────────────────────────────────────────────────────────────
    // Status transition tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_SetsCompletedAtOnSuccess()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));
        order.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OrderAsync_SetsCompletedAtOnFailure()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Fail("disk full"));

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.CompletedAt.Should().NotBeNull();
        order.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OrderAsync_SetsCreatedAtOnOrder()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        var order = await _factory.OrderAsync(request);

        order.Id.Should().NotBe(Guid.Empty);
        order.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task OrderAsync_PersistsStatusTransitions()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        await _factory.OrderAsync(request);

        // Verify status updates were called for each transition
        _orderRepository.Verify(r => r.UpdateStatusAsync(
            It.IsAny<Guid>(), OrderStatus.Resolving, It.IsAny<DateTime?>(), null), Times.Once);
        _orderRepository.Verify(r => r.UpdateStatusAsync(
            It.IsAny<Guid>(), OrderStatus.Validating, null, null), Times.Once);
        _orderRepository.Verify(r => r.UpdateStatusAsync(
            It.IsAny<Guid>(), OrderStatus.Restoring, null, null), Times.Once);
        _orderRepository.Verify(r => r.UpdateStatusAsync(
            It.IsAny<Guid>(), OrderStatus.Delivered, null, It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task OrderAsync_CallsFailAsyncOnFailure()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Fail("disk full"));

        var request = MakeRequest(template.Id);

        await _factory.OrderAsync(request);

        _orderRepository.Verify(r => r.FailAsync(It.IsAny<Guid>(), It.Is<string>(s => s.Contains("Restore failed"))), Times.Once);
    }

    [Fact]
    public async Task OrderAsync_RecordsProvisionedDatabaseOnSuccess()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id);

        await _factory.OrderAsync(request);

        _orderRepository.Verify(r => r.RecordProvisionedDatabaseAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), defaultConn.Id, template.Id), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────
    // Argument validation tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_ThrowsWhenRequestIsNull()
    {
        var act = () => _factory.OrderAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OrderAsync_ThrowsWhenTemplateIdIsEmpty()
    {
        var request = new OrderRequest
        {
            TemplateId = Guid.Empty,
            TargetDatabaseName = "test_db"
        };

        var act = () => _factory.OrderAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TemplateId*");
    }

    [Fact]
    public async Task OrderAsync_ThrowsWhenTargetDatabaseNameIsEmpty()
    {
        var request = new OrderRequest
        {
            TemplateId = Guid.NewGuid(),
            TargetDatabaseName = ""
        };

        var act = () => _factory.OrderAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TargetDatabaseName*");
    }

    // ───────────────────────────────────────────────────────────────
    // Dependency ordering tests
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_RespectsGroupDependencies()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        var group1 = MakeGroup(layer: 1, order: 1);
        var group2 = new ScriptGroup
        {
            Id = Guid.NewGuid(),
            Name = "Dependent Group",
            Layer = 2,
            Order = 1,
            Dependencies = new List<Guid> { group1.Id },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group1.Id)).ReturnsAsync(group1);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group2.Id)).ReturnsAsync(group2);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var script1 = MakeScript(group1.Id, 1);
        var script2 = MakeScript(group2.Id, 1);

        _scriptLibrary.Setup(s => s.ListScriptsAsync(group1.Id))
            .ReturnsAsync(new List<Script> { script1 });
        _scriptLibrary.Setup(s => s.ListScriptsAsync(group2.Id))
            .ReturnsAsync(new List<Script> { script2 });

        _scriptExecutor.Setup(e => e.ExecuteAsync(It.IsAny<Script>(), It.IsAny<ParamDict>(), It.IsAny<string>()))
            .ReturnsAsync(ScriptResult.Ok("test", 1, TimeSpan.Zero));

        // Pass groups out of order to verify sorting
        var request = MakeRequest(template.Id, scriptGroupIds: new[] { group2.Id, group1.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));

        var calls = _scriptExecutor.Invocations
            .Where(i => i.Method.Name == nameof(IScriptExecutor.ExecuteAsync))
            .Select(i => (Script)i.Arguments[0])
            .ToList();

        // group1 (layer 1) should execute before group2 (layer 2)
        calls[0].Id.Should().Be(script1.Id);
        calls[1].Id.Should().Be(script2.Id);
    }

    [Fact]
    public async Task OrderAsync_FailsWhenCircularDependencyDetected()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);

        var groupA = MakeGroup(layer: 1, order: 1);
        var groupB = MakeGroup(layer: 1, order: 2);
        groupA.Dependencies = new List<Guid> { groupB.Id };
        groupB.Dependencies = new List<Guid> { groupA.Id };

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(groupA.Id)).ReturnsAsync(groupA);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(groupB.Id)).ReturnsAsync(groupB);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(SqlResult.Ok());

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { groupA.Id, groupB.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Failed));
        order.ErrorMessage.Should().Contain("Circular dependency");
    }

    // ───────────────────────────────────────────────────────────────
    // Full pipeline call sequence test
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderAsync_FullPipeline_CallsAllStepsInOrder()
    {
        var template = MakeTemplate();
        var defaultConn = MakeConnection(isDefault: true);
        var group = MakeGroup(layer: 1, order: 1);
        var script = MakeScript(group.Id, 1);

        _connectionRegistry.Setup(r => r.GetDefaultAsync()).ReturnsAsync(defaultConn);
        _templateRepository.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _scriptGroupRepository.Setup(r => r.GetGroupAsync(group.Id)).ReturnsAsync(group);
        _databaseProvider.Setup(p => p.RestoreBackupAsync(
                template.FilePath, It.IsAny<string>(), defaultConn.ConnectionString))
            .ReturnsAsync(SqlResult.Ok(1));
        _scriptLibrary.Setup(s => s.ListScriptsAsync(group.Id))
            .ReturnsAsync(new List<Script> { script });
        _scriptExecutor.Setup(e => e.ExecuteAsync(
                script, It.IsAny<ParamDict>(), defaultConn.ConnectionString))
            .ReturnsAsync(ScriptResult.Ok(script.Name, 5, TimeSpan.FromMilliseconds(100)));

        var request = MakeRequest(template.Id, scriptGroupIds: new[] { group.Id });

        var order = await _factory.OrderAsync(request);

        order.Status.Should().Be(nameof(OrderStatus.Delivered));

        // Verify all steps were called
        _orderRepository.Verify(r => r.CreateAsync(It.IsAny<OrderRequest>()), Times.Once);
        _connectionRegistry.Verify(r => r.GetDefaultAsync(), Times.Once);
        _templateRepository.Verify(r => r.GetByIdAsync(template.Id), Times.Once);
        _scriptGroupRepository.Verify(r => r.GetGroupAsync(group.Id), Times.Once);
        _databaseProvider.Verify(p => p.RestoreBackupAsync(
            template.FilePath, It.IsAny<string>(), defaultConn.ConnectionString), Times.Once);
        _scriptLibrary.Verify(s => s.ListScriptsAsync(group.Id), Times.Once);
        _scriptExecutor.Verify(e => e.ExecuteAsync(
            script, It.IsAny<ParamDict>(), defaultConn.ConnectionString), Times.Once);
        _orderRepository.Verify(r => r.RecordProvisionedDatabaseAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), defaultConn.Id, template.Id), Times.Once);
    }
}
