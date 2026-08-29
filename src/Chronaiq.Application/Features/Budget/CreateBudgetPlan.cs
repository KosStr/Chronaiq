using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Budget.Models;
using Chronaiq.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Budget;

/// <summary>Creates a budget plan for a user.</summary>
public sealed record CreateBudgetPlanCommand(
    Guid UserId,
    string Name,
    decimal MonthlyIncome,
    decimal MonthlySavingsTarget,
    DateOnly StartDate,
    DateOnly? EndDate) : IRequest<BudgetPlanDto>;

public sealed class CreateBudgetPlanHandler(IApplicationDbContext db)
    : IRequestHandler<CreateBudgetPlanCommand, BudgetPlanDto>
{
    public async Task<BudgetPlanDto> Handle(CreateBudgetPlanCommand request, CancellationToken cancellationToken)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var plan = new BudgetPlan
        {
            UserId = request.UserId,
            Name = request.Name.Trim(),
            MonthlyIncome = request.MonthlyIncome,
            MonthlySavingsTarget = request.MonthlySavingsTarget,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        db.BudgetPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        return BudgetPlanDto.FromEntity(plan);
    }
}
