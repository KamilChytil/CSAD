using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Application.Exchange.Services;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed class ExecuteExchangeCommandHandler(
    IExchangeRateService exchangeRateService,
    IExchangeTransactionRepository transactionRepository,
    IAccountsServiceClient accountsClient,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ExecuteExchangeCommand, ExchangeTransactionResponse>
{
    public async Task<ExchangeTransactionResponse> Handle(
        ExecuteExchangeCommand request, CancellationToken cancellationToken)
    {
        var rateResult = await exchangeRateService.GetRateAsync(request.FromCurrency, request.ToCurrency, cancellationToken);
        if (rateResult is null)
            throw new InvalidOperationException("Exchange rate unavailable. Please try again later.");

        var targetAmount = Math.Round(request.Amount * rateResult.Rate, 2);
        if (targetAmount <= 0)
            throw new InvalidOperationException("Calculated target amount is zero or negative.");

        var withdrawDesc = $"Smena {request.FromCurrency.ToUpperInvariant()} -> {request.ToCurrency.ToUpperInvariant()}";
        var withdrawOk = await accountsClient.WithdrawAsync(
            request.SourceAccountId, request.Amount, request.FromCurrency, withdrawDesc, cancellationToken);

        if (!withdrawOk)
            throw new InvalidOperationException("Withdrawal failed. Insufficient funds or account inactive.");

        var depositDesc = $"Smena {request.FromCurrency.ToUpperInvariant()} -> {request.ToCurrency.ToUpperInvariant()}";
        var depositOk = await accountsClient.DepositAsync(
            request.TargetAccountId, targetAmount, request.ToCurrency, depositDesc, cancellationToken);

        if (!depositOk)
        {
            // Compensate: deposit back to source
            await accountsClient.DepositAsync(
                request.SourceAccountId, request.Amount, request.FromCurrency,
                "Kompenzace - neuspesna smena", cancellationToken);
            throw new InvalidOperationException("Deposit failed, funds returned.");
        }

        var transaction = ExchangeTransaction.Create(
            request.UserId, request.SourceAccountId, request.TargetAccountId,
            request.FromCurrency, request.ToCurrency, request.Amount, targetAmount, rateResult.Rate);

        await transactionRepository.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExchangeTransactionResponse(
            transaction.Id, transaction.SourceAccountId, transaction.TargetAccountId,
            transaction.FromCurrency.ToString(), transaction.ToCurrency.ToString(),
            transaction.SourceAmount, transaction.TargetAmount,
            transaction.ExchangeRate, transaction.CreatedAt);
    }
}
