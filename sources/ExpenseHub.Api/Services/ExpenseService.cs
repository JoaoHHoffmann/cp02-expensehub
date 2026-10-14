using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
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

    public async Task<ExpenseResponse> CreateAsync(ClaimsPrincipal user, ExpenseRequest request, CancellationToken cancellationToken)
    {
        ExpenseAccess.EnsureEmployee(user);
        int categoryId = request.CategoryId.GetValueOrDefault();
        await EnsureCategoryExistsAsync(categoryId, cancellationToken);

        Expense expense = Expense.Create(
            user.GetUserId(),
            categoryId,
            request.Description ?? string.Empty,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            _clock.GetUtcNow().UtcDateTime);

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseResponse.FromEntity(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(Guid id, ClaimsPrincipal user, ExpenseRequest request, CancellationToken cancellationToken)
    {
        // Só o proprietário enxerga o próprio rascunho; para os demais, o recurso não existe (404).
        Expense expense = await _db.Expenses.Where(ExpenseAccess.OwnerFilter(user))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(DomainErrorType.NotFound, "Reembolso não encontrado.");

        int categoryId = request.CategoryId.GetValueOrDefault();
        await EnsureCategoryExistsAsync(categoryId, cancellationToken);

        expense.Update(
            user.GetUserId(),
            categoryId,
            request.Description ?? string.Empty,
            request.Amount.GetValueOrDefault(),
            request.ExpenseDate.GetValueOrDefault(),
            _clock.GetUtcNow().UtcDateTime);

        // Alteração e histórico são gravados juntos, no mesmo SaveChanges.
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseResponse.FromEntity(expense);
    }

    public async Task<ExpenseResponse> SubmitAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        Expense expense = await _db.Expenses.Where(ExpenseAccess.OwnerFilter(user))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(DomainErrorType.NotFound, "Reembolso não encontrado.");

        expense.Submit(user.GetUserId(), _clock.GetUtcNow().UtcDateTime);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpenseResponse.FromEntity(expense);
    }

    public async Task<List<ExpenseResponse>> ListAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        List<Expense> expenses = await _db.Expenses.AsNoTracking()
            .Where(ExpenseAccess.ReadFilter(user))
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        return expenses.Select(ExpenseResponse.FromEntity).ToList();
    }

    public async Task<ExpenseResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        Expense expense = await _db.Expenses.AsNoTracking()
            .Where(ExpenseAccess.ReadFilter(user))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException(DomainErrorType.NotFound, "Reembolso não encontrado.");

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
