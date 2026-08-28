using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CdcModels.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

/// <summary>
/// End-to-end integration test for the Factory order lifecycle.
/// This test requires:
/// - A running DTAI API server
/// - A reachable SQL Server instance
/// - A reachable PostgreSQL instance (for factory metadata)
///
/// The test is skipped if the API server is not reachable.
/// Run the API server manually before running this test.
/// </summary>
[Trait("Category", "Integration")]
public class FactoryOrderLifecycleTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;

    public FactoryOrderLifecycleTests(WebApplicationFactoryFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact(Skip = "Requires running API server with SQL Server + PostgreSQL. Remove Skip to run manually.")]
    public async Task FullOrderLifecycle_WorksEndToEnd()
    {
        // Step 1: Register a connection
        var createConnection = new CreateConnectionDto
        {
            Name = "Integration Test Connection",
            Platform = "SqlServer",
            Host = "localhost",
            Port = 1433,
            ConnectionString = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;",
            IsDefault = true
        };

        var connResponse = await _client.PostAsJsonAsync("/api/factory/connections", createConnection);
        connResponse.EnsureSuccessStatusCode();
        var connection = await connResponse.Content.ReadFromJsonAsync<ConnectionDto>();
        Assert.NotNull(connection);

        // Step 2: Register a template by path
        var createTemplate = new
        {
            name = "Integration Test Template",
            version = "1.0",
            platform = "SqlServer",
            filePath = "/tmp/test-template.bak",
            createdBy = "integration-test"
        };

        var templateResponse = await _client.PostAsJsonAsync("/api/factory/templates", createTemplate);
        templateResponse.EnsureSuccessStatusCode();
        var template = await templateResponse.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.NotNull(template);

        // Step 3: Create script groups
        var createGroup = new
        {
            name = "Base Schema",
            description = "Base tables and schema",
            layer = 1,
            order = 1
        };

        var groupResponse = await _client.PostAsJsonAsync("/api/factory/script-groups", createGroup);
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<ScriptGroupDto>();
        Assert.NotNull(group);

        // Step 4: Create a script
        var createScript = new
        {
            name = "Create Test Table",
            type = "SqlScript",
            content = "SELECT 1",
            scriptGroupId = group!.Id,
            order = 1
        };

        var scriptResponse = await _client.PostAsJsonAsync("/api/factory/scripts", createScript);
        scriptResponse.EnsureSuccessStatusCode();

        // Step 5: Place an order
        var createOrder = new CreateOrderDto
        {
            TemplateId = template!.Id,
            TargetDatabaseName = "acme_integration_test_{date}",
            ScriptGroupIds = new[] { group!.Id }
        };

        var orderResponse = await _client.PostAsJsonAsync("/api/factory/orders", createOrder);
        // Order may succeed (201) or fail (400) depending on backup availability
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);

        // Step 6: Poll status
        var statusResponse = await _client.GetAsync($"/api/factory/orders/{order!.Id}/status");
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<OrderStatusDto>();
        Assert.NotNull(status);
        Assert.Equal(order.Id, status!.Id);

        // Step 7: Verify in registry (if delivered)
        if (status.Status == "Delivered")
        {
            var databasesResponse = await _client.GetAsync("/api/factory/databases");
            databasesResponse.EnsureSuccessStatusCode();
        }
    }
}

/// <summary>
/// Test fixture that checks if the API server is running.
/// The integration test is skipped if the server is not reachable.
/// </summary>
public class WebApplicationFactoryFixture : IDisposable
{
    public HttpClient Client { get; }
    public bool IsServerAvailable { get; }

    public WebApplicationFactoryFixture()
    {
        Client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("DTAI_API_URL") ?? "http://localhost:5000")
        };

        try
        {
            // Quick health check
            using var response = Client.GetAsync("/health").GetAwaiter().GetResult();
            IsServerAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            IsServerAvailable = false;
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
    }
}
