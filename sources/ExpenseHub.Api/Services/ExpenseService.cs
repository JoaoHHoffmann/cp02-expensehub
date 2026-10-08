using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Services;

internal sealed class ExpenseService
{
    private readonly ExpenseHubDbContext _db;
    private readonly TimeProvider _clock;

    public ExpenseService(ExpenseHubDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ExpenseResponse> CreateAsync(string userId, ExpenseRequest request, CancellationToken cancellationToken)
    {
        int categoryId = request.CategoryId.GetValueOrDefault();
        await EnsureCategoryExistsAsync(categoryId, cancellationToken);

        Expense expense = Expense.Create(
            userId,
            categoryId,
            request.Description ?? string.Empty,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            _clock.GetUtcNow().UtcDateTime);

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseResponse.FromEntity(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(Guid id, string userId, ExpenseRequest request, CancellationToken cancellationToken)
    {
        // Só o proprietário enxerga o próprio rascunho; para os demais, o recurso não existe (404).
        Expense expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.OwnerId == userId, cancellationToken)
            ?? throw new DomainException(DomainErrorType.NotFound, "Reembolso não encontrado.");

        int categoryId = request.CategoryId.GetValueOrDefault();
        await EnsureCategoryExistsAsync(categoryId, cancellationToken);

        expense.Update(
            userId,
            categoryId,
            request.Description ?? string.Empty,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            _clock.GetUtcNow().UtcDateTime);

        // Alteração e histórico são gravados juntos, no mesmo SaveChanges.
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseResponse.FromEntity(expense);
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await _db.ExpenseCategories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new DomainException(DomainErrorType.Validation, "Categoria inexistente.");
        }
    }
}
