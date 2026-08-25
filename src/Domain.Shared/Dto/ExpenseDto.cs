namespace ResellerSystem.Domain.Shared.Dto;

public sealed class ExpenseDto
{
    public required Guid Id { get; init; }
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public Guid? PurchaseId { get; init; }
    public Guid? ItemId { get; init; }
    public Guid? SaleId { get; init; }
    public Guid? ReturnId { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Comment { get; init; }
}

public sealed class CreateExpenseRequest
{
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public Guid? PurchaseId { get; init; }
    public Guid? ItemId { get; init; }
    public Guid? SaleId { get; init; }
    public Guid? ReturnId { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Comment { get; init; }
}
