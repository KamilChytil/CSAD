using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Application.Services;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Payments.Commands.SendPayment;

public sealed class SendPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IAccountsServiceClient accountsClient,
    INotificationClient notificationClient,
    IIdentityClient identityClient,
    IUnitOfWork unitOfWork) : IRequestHandler<SendPaymentCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(SendPaymentCommand request, CancellationToken ct)
    {
        // 1. Validate sender account exists
        var senderAccount = await accountsClient.GetAccountByIdAsync(request.SenderAccountId, ct)
            ?? throw new InvalidOperationException("Sender account not found.");

        if (!senderAccount.IsActive)
            throw new InvalidOperationException("Sender account is not active.");

        // 2. Parse currency
        if (!Enum.TryParse<Currency>(request.Currency, true, out var currency))
            throw new ArgumentException($"Invalid currency: {request.Currency}");

        // 3. Check sufficient balance
        if (senderAccount.Balance < request.Amount)
            throw new InvalidOperationException("Insufficient funds.");

        // 3a. Enforce financial & security limits
        var accountLimits = await accountsClient.GetAccountLimitsAsync(request.SenderAccountId, ct);
        if (accountLimits is not null)
        {
            // Night restriction (23:00–06:00 UTC)
            LimitEnforcementService.EnforceNightRestriction(nightEnabled: false);

            // Single transaction limit
            LimitEnforcementService.EnforceSingleTransactionLimit(request.Amount, accountLimits.SingleTransactionLimit);

            // Calculate daily and monthly totals from payment history
            var today = DateTime.UtcNow.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var (dailyTotal, dailyCount) = await paymentRepository.GetSentTotalsAsync(request.SenderAccountId, today, ct);
            var (monthlyTotal, _) = await paymentRepository.GetSentTotalsAsync(request.SenderAccountId, monthStart, ct);

            LimitEnforcementService.EnforceDailyLimit(dailyTotal, request.Amount, accountLimits.DailyTransactionLimit);
            LimitEnforcementService.EnforceMonthlyLimit(monthlyTotal, request.Amount, accountLimits.MonthlyTransactionLimit);
            LimitEnforcementService.EnforceDailyCount(dailyCount, accountLimits.DailyTransactionCount);
        }

        // 3b. Check spending limits (child account protection)
        var limits = await accountsClient.GetSpendingLimitAsync(request.SenderAccountId, ct);
        if (limits is { RequiresApproval: true, ApprovalThreshold: not null }
            && request.Amount > limits.ApprovalThreshold.Value)
        {
            if (!Enum.TryParse<Currency>(request.Currency, true, out var currencyForPending))
                throw new ArgumentException($"Invalid currency: {request.Currency}");

            // Create pending transaction for parent approval
            var pending = await accountsClient.CreatePendingTransactionAsync(
                request.SenderAccountId, request.Amount, request.Currency,
                $"Platba → {request.RecipientAccountNumber}: {request.Description ?? ""}".Trim(),
                senderAccount.OwnerId, ct);

            if (pending is null)
                throw new InvalidOperationException("Failed to create pending transaction.");

            // Create payment record with PendingApproval status
            var pendingPayment = Payment.Create(
                senderAccountId: request.SenderAccountId,
                senderAccountNumber: senderAccount.AccountNumber,
                recipientAccountNumber: request.RecipientAccountNumber,
                amount: request.Amount,
                currency: currencyForPending,
                type: request.IsInstant ? PaymentType.Instant : PaymentType.Standard,
                description: request.Description);

            pendingPayment.MarkPendingApproval();
            await paymentRepository.AddAsync(pendingPayment, ct);
            await unitOfWork.SaveChangesAsync(ct);

            // Send notification to parent
            var childUser = await identityClient.GetUserAsync(senderAccount.OwnerId, ct);
            if (childUser?.ParentId is not null)
            {
                await notificationClient.SendAsync(
                    childUser.ParentId.Value,
                    "TransactionPending",
                    "Platba čeká na schválení",
                    $"{childUser.FirstName} chce zaplatit {request.Amount} {request.Currency} → {request.RecipientAccountNumber}",
                    pending.Id, "PendingTransaction", ct);
            }

            return MapToResponse(pendingPayment);
        }

        // 4. Determine payment type
        var paymentType = request.IsInstant ? PaymentType.Instant : PaymentType.Standard;

        // 5. Look up recipient account and validate it exists for FairBank accounts
        var recipientAccount = await accountsClient.GetAccountByNumberAsync(request.RecipientAccountNumber, ct);

        // If the recipient account number belongs to FairBank (bank code 8888) it MUST exist in the DB.
        // Sending to a non-existent FairBank account would cause money to vanish, so we reject it.
        var recipientBankCode = request.RecipientAccountNumber.Contains('/')
            ? request.RecipientAccountNumber[(request.RecipientAccountNumber.LastIndexOf('/') + 1)..]
            : string.Empty;
        if (recipientBankCode == "8888" && recipientAccount is null)
            throw new InvalidOperationException($"Recipient account '{request.RecipientAccountNumber}' does not exist.");

        if (recipientAccount is not null && senderAccount.OwnerId == recipientAccount.OwnerId)
            paymentType = PaymentType.InternalTransfer;

        // 6. Create payment record
        var payment = Payment.Create(
            senderAccountId: request.SenderAccountId,
            senderAccountNumber: senderAccount.AccountNumber,
            recipientAccountNumber: request.RecipientAccountNumber,
            amount: request.Amount,
            currency: currency,
            type: paymentType,
            description: request.Description,
            recipientAccountId: recipientAccount?.Id);

        // Auto-categorize based on description
        var category = PaymentCategorizer.Categorize(request.Description);
        payment.SetCategory(category);

        await paymentRepository.AddAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // 7. Execute the transfer via Accounts service
        var withdrawOk = await accountsClient.WithdrawAsync(
            request.SenderAccountId, request.Amount, request.Currency,
            $"Platba → {request.RecipientAccountNumber}: {request.Description ?? ""}".Trim(), ct);

        if (!withdrawOk)
        {
            payment.MarkFailed("Withdraw from sender account failed.");
            await paymentRepository.UpdateAsync(payment, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapToResponse(payment);
        }

        // 8. Deposit to recipient (if internal)
        if (recipientAccount is not null)
        {
            var depositOk = await accountsClient.DepositAsync(
                recipientAccount.Id, request.Amount, request.Currency,
                $"Příchozí platba ← {senderAccount.AccountNumber}: {request.Description ?? ""}".Trim(), ct);

            if (!depositOk)
            {
                // Compensate — refund sender
                await accountsClient.DepositAsync(
                    request.SenderAccountId, request.Amount, request.Currency,
                    $"Vrácení platby (selhání převodu na {request.RecipientAccountNumber})", ct);

                payment.MarkFailed("Deposit to recipient account failed.");
                await paymentRepository.UpdateAsync(payment, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return MapToResponse(payment);
            }
        }

        // 9. Mark completed
        payment.MarkCompleted(recipientAccount?.Id);
        await paymentRepository.UpdateAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment p) => new(
        p.Id, p.SenderAccountId, p.RecipientAccountId,
        p.SenderAccountNumber, p.RecipientAccountNumber,
        p.Amount, p.Currency.ToString(), p.Description,
        p.Type.ToString(), p.Status.ToString(),
        p.Category.ToString(),
        p.CreatedAt, p.CompletedAt, p.FailureReason);
}
