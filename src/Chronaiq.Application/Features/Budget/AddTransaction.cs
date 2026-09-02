using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Budget.Models;
using Chronaiq.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Budget;

/// <summary>Appends a ledger entry to an existing budget plan.</summary>
public sealed record AddTransactionCommand(
    Guid BudgetPlanId,
    decimal Amount,
    string MerchantName,
    string Category,
    DateTimeOffset TransactionDate) : IRequest<TransactionDto>;

public sealed class AddTransactionHandler(IApplicationDbContext db)
    : IRequestHandler<AddTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(AddTransactionCommand request, CancellationToken cancellationToken)
    {
        var planExists = await db.BudgetPlans.AnyAsync(p => p.Id == request.BudgetPlanId, cancellationToken);
        if (!planExists)
        {
            throw new NotFoundException(nameof(BudgetPlan), request.BudgetPlanId);
        }

        var transaction = new Transaction
        {
            BudgetPlanId = request.BudgetPlanId,
            Amount = request.Amount,
            MerchantName = request.MerchantName.Trim(),
            Category = request.Category.Trim(),
            TransactionDate = request.TransactionDate
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return TransactionDto.FromEntity(transaction);
    }
}
