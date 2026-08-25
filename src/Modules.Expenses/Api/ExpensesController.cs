using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Expenses.Application;

namespace ResellerSystem.Modules.Expenses.Api;

[ApiController]
[Authorize]
[Route("api/v1/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpensesService _service;
    public ExpensesController(IExpensesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> List(
        [FromQuery] Guid? saleId, [FromQuery] Guid? purchaseId, [FromQuery] Guid? itemId, CancellationToken ct) =>
        Ok(await _service.ListAsync(saleId, purchaseId, itemId, ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseRequest request, CancellationToken ct) =>
        Ok(await _service.CreateAsync(request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
