using System.ComponentModel.DataAnnotations;
using Xunit;
using cdc_api.Models;

namespace cdc_api.Tests.Models;

/// <summary>
/// Unit tests for CDC request and response models
/// </summary>
public class CdcModelsTests
{
    /// <summary>
    /// Test that StartCdcRequest validates required SessionName
    /// </summary>
    [Fact]
    public void StartCdcRequest_EmptySessionName_FailsValidation()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = ""
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("SessionName"));
    }

    /// <summary>
    /// Test that StartCdcRequest with valid SessionName passes validation
    /// </summary>
    [Fact]
    public void StartCdcRequest_ValidSessionName_PassesValidation()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = "valid-session"
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Test that StartCdcRequest allows null table filters
    /// </summary>
    [Fact]
    public void StartCdcRequest_NullTableFilters_PassesValidation()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = "test-session",
            TablesToInclude = null,
            TablesToExclude = null
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Test that StopCdcRequest validates required fields
    /// </summary>
    [Fact]
    public void StopCdcRequest_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        var request = new StopCdcRequest
        {
            SessionName = "",
            CaptureName = ""
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("SessionName"));
        Assert.Contains(validationResults, v => v.MemberNames.Contains("CaptureName"));
    }

    /// <summary>
    /// Test that StopCdcRequest with valid fields passes validation
    /// </summary>
    [Fact]
    public void StopCdcRequest_ValidFields_PassesValidation()
    {
        // Arrange
        var request = new StopCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "test-capture",
            CaptureType = "Baseline"
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Test that StopCdcRequest has default CaptureType
    /// </summary>
    [Fact]
    public void StopCdcRequest_DefaultCaptureType_IsBaseline()
    {
        // Arrange & Act
        var request = new StopCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "test-capture"
        };

        // Assert
        Assert.Equal("Baseline", request.CaptureType);
    }

    /// <summary>
    /// Test that CaptureCdcRequest validates required fields
    /// </summary>
    [Fact]
    public void CaptureCdcRequest_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        var request = new CaptureCdcRequest
        {
            SessionName = "",
            CaptureName = ""
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("SessionName"));
        Assert.Contains(validationResults, v => v.MemberNames.Contains("CaptureName"));
    }

    /// <summary>
    /// Test that CaptureCdcRequest has default CaptureType
    /// </summary>
    [Fact]
    public void CaptureCdcRequest_DefaultCaptureType_IsIntermediate()
    {
        // Arrange & Act
        var request = new CaptureCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "test-capture"
        };

        // Assert
        Assert.Equal("Intermediate", request.CaptureType);
    }

    /// <summary>
    /// Test that StartCdcResponse initializes collections properly
    /// </summary>
    [Fact]
    public void StartCdcResponse_DefaultInitialization_HasEmptyCollections()
    {
        // Arrange & Act
        var response = new StartCdcResponse();

        // Assert
        Assert.NotNull(response.TablesEnabled);
        Assert.NotNull(response.TablesSkipped);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.TablesEnabled);
        Assert.Empty(response.TablesSkipped);
        Assert.Empty(response.Errors);
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.SessionName);
        Assert.Equal(string.Empty, response.Message);
    }

    /// <summary>
    /// Test that StopCdcResponse initializes collections properly
    /// </summary>
    [Fact]
    public void StopCdcResponse_DefaultInitialization_HasEmptyCollections()
    {
        // Arrange & Act
        var response = new StopCdcResponse();

        // Assert
        Assert.NotNull(response.TablesWithChanges);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.TablesWithChanges);
        Assert.Empty(response.Errors);
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.SessionName);
        Assert.Equal(string.Empty, response.CaptureName);
        Assert.Equal(string.Empty, response.Message);
        Assert.Equal(0, response.TotalRecords);
        Assert.Null(response.CaptureId);
    }

    /// <summary>
    /// Test that CaptureCdcResponse initializes collections properly
    /// </summary>
    [Fact]
    public void CaptureCdcResponse_DefaultInitialization_HasEmptyCollections()
    {
        // Arrange & Act
        var response = new CaptureCdcResponse();

        // Assert
        Assert.NotNull(response.TablesWithChanges);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.TablesWithChanges);
        Assert.Empty(response.Errors);
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.SessionName);
        Assert.Equal(string.Empty, response.CaptureName);
        Assert.Equal(string.Empty, response.CaptureType);
        Assert.Equal(string.Empty, response.Message);
        Assert.Equal(0, response.TotalRecords);
        Assert.Null(response.CaptureId);
    }

    /// <summary>
    /// Test that response models can be populated with data
    /// </summary>
    [Fact]
    public void StartCdcResponse_PopulatedWithData_ReturnsCorrectValues()
    {
        // Arrange & Act
        var response = new StartCdcResponse
        {
            Success = true,
            SessionName = "test-session",
            Message = "CDC enabled successfully",
            TablesEnabled = new List<string> { "dbo.Orders", "dbo.Customers" },
            TablesSkipped = new List<string> { "dbo.AuditLog" },
            Errors = new List<string> { "Warning: Table dbo.TempTable has no primary key" }
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("test-session", response.SessionName);
        Assert.Equal("CDC enabled successfully", response.Message);
        Assert.Equal(2, response.TablesEnabled.Count);
        Assert.Contains("dbo.Orders", response.TablesEnabled);
        Assert.Contains("dbo.Customers", response.TablesEnabled);
        Assert.Single(response.TablesSkipped);
        Assert.Contains("dbo.AuditLog", response.TablesSkipped);
        Assert.Single(response.Errors);
    }

    /// <summary>
    /// Test that StopCdcResponse can be populated with data
    /// </summary>
    [Fact]
    public void StopCdcResponse_PopulatedWithData_ReturnsCorrectValues()
    {
        // Arrange & Act
        var captureId = Guid.NewGuid().ToString();
        var response = new StopCdcResponse
        {
            Success = true,
            SessionName = "test-session",
            CaptureName = "baseline-capture",
            Message = "CDC data captured successfully",
            TablesWithChanges = new List<string> { "dbo_Orders", "dbo_Customers" },
            TotalRecords = 150,
            CaptureId = captureId
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("test-session", response.SessionName);
        Assert.Equal("baseline-capture", response.CaptureName);
        Assert.Equal("CDC data captured successfully", response.Message);
        Assert.Equal(2, response.TablesWithChanges.Count);
        Assert.Equal(150, response.TotalRecords);
        Assert.Equal(captureId, response.CaptureId);
    }

    /// <summary>
    /// Helper method to validate a model using data annotations
    /// </summary>
    /// <param name="model">The model to validate</param>
    /// <returns>List of validation results</returns>
    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}