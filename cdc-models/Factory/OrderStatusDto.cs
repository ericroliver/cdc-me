namespace CdcModels.Factory;

/// <summary>
/// Lightweight order status for polling — just the essentials.
/// </summary>
public class OrderStatusDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
