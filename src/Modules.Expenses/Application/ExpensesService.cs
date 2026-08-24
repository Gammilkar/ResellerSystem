using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Expenses.Data;
using ResellerSystem.Modules.Expenses.Domain;
using ResellerSystem.Server.Application.Exceptions;

namespace ResellerSystem.Modules.Expenses.Application;

public interface IExpensesService
{
    Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseDto>> ListAsync(Guid? saleId, Guid? purchaseId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class ExpensesService : IExpensesService
{
    private readonly IExpensesDbContextFactory _dbContextFactory;

    public ExpensesService(IExpensesDbContextFactory dbContextFactory) => _dbContextFactory = dbContextFactory;

    public async Task<ExpenseDto> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExpenseType))
            throw new ValidationFailedException(new[] { "Expense type is required." });
        if (request.Amount < 0)
            throw new ValidationFailedException(new[] { "Amount cannot be negative." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var expense = Expense.CreateNew(request.ExpenseType, request.Amount, request.ExpenseDate,
            request.PurchaseId, request.ItemId, request.SaleId, request.ReturnId, request.PaymentMethod, request.Comment);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        return ToDto(expense);
    }

    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(Guid? saleId, Guid? purchaseId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var query = db.Expenses.AsQueryable();
        if (saleId is not null) query = query.Where(e => e.SaleId == saleId);
        if (purchaseId is not null) query = query.Where(e => e.PurchaseId == purchaseId);

        return await query.OrderByDescending(e => e.ExpenseDate).Select(e => ToDto(e)).ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("EXPENSE_NOT_FOUND", "Expense was not found.");
        expense.SoftDelete();
        await db.SaveChangesAsync(ct);
    }

    private static ExpenseDto ToDto(Expense e) => new()
    {
        Id = e.Id,
        ExpenseType = e.ExpenseType,
        Amount = e.Amount,
        ExpenseDate = e.ExpenseDate,
        PurchaseId = e.PurchaseId,
        ItemId = e.ItemId,
        SaleId = e.SaleId,
        ReturnId = e.ReturnId,
        Comment = e.Comment
    };
}
