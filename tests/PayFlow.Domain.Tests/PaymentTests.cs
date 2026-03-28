using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Events;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;
using Xunit;

namespace PayFlow.Domain.Tests;

public class PaymentTests
{
    private readonly TenantId _tenantId = new(Guid.NewGuid());
    private readonly CustomerId _customerId = new(Guid.NewGuid());
    private readonly IdempotencyKey _idempotencyKey = new("test-key-001");

    [Fact]
    public void Create_Payment_WithValidData_ShouldSucceed()
    {
        var amount = new Money(100m, Currency.GBP);
        var paymentMethod = new PaymentMethodSnapshot("card", "4242", "visa");

        var payment = Payment.Create(
            _tenantId,
            _idempotencyKey,
            amount,
            Currency.GBP,
            PaymentMode.Live,
            _customerId,
            paymentMethod);

        Assert.Equal(PaymentStatus.Created, payment.Status);
        Assert.Equal(_tenantId, payment.TenantId);
        Assert.Equal(PaymentMode.Live, payment.Mode);
        Assert.Equal(amount, payment.Amount);
    }

    [Fact]
    public void Create_Payment_WithNegativeAmount_ShouldThrow()
    {
        var paymentMethod = new PaymentMethodSnapshot("card", "4242", "visa");

        Assert.Throws<ArgumentException>(() => Payment.Create(
            _tenantId,
            _idempotencyKey,
            new Money(-100m, Currency.GBP),
            Currency.GBP,
            PaymentMode.Live,
            _customerId,
            paymentMethod));
    }

    [Fact]
    public void TenantId_ShouldBeImmutable()
    {
        var property = typeof(Payment).GetProperty("TenantId");
        var setter = property?.SetMethod;

        Assert.NotNull(setter);
        Assert.True(setter?.IsPrivate);
    }

    [Fact]
    public void Mode_ShouldBeImmutable()
    {
        var property = typeof(Payment).GetProperty("Mode");
        var setter = property?.SetMethod;

        Assert.NotNull(setter);
        Assert.True(setter?.IsPrivate);
    }

    [Fact]
    public void Authorise_FromCreated_ShouldTransitionToAuthorised()
    {
        var payment = CreateTestPayment();
        var gatewayRef = "gw_123";

        payment.Authorise(gatewayRef);

        Assert.Equal(PaymentStatus.Authorised, payment.Status);
        Assert.Equal(gatewayRef, payment.GatewayReference);
        Assert.Contains(payment.DomainEvents, e => e is PaymentAuthorised);
    }

    [Fact]
    public void Authorise_FromAuthorised_ShouldThrow()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");

        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Authorise("gw_456"));
    }

    [Fact]
    public void Capture_FromAuthorised_ShouldTransitionToCaptured()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");

        payment.Capture();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentCaptured);
    }

    [Fact]
    public void Capture_FromCreated_ShouldThrow()
    {
        var payment = CreateTestPayment();

        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Capture());
    }

    [Fact]
    public void Settle_FromCaptured_ShouldTransitionToSettled()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();

        payment.Settle();

        Assert.Equal(PaymentStatus.Settled, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentSettled);
    }

    [Fact]
    public void Fail_FromCreated_ShouldTransitionToFailed()
    {
        var payment = CreateTestPayment();

        payment.Fail("Insufficient funds");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Insufficient funds", payment.FailureReason);
        Assert.Contains(payment.DomainEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void Fail_FromAuthorised_ShouldTransitionToFailed()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");

        payment.Fail("Card declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void Cancel_FromCreated_ShouldTransitionToCancelled()
    {
        var payment = CreateTestPayment();

        payment.Cancel();

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentCancelled);
    }

    [Fact]
    public void Cancel_FromCaptured_ShouldThrow()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();

        Assert.Throws<InvalidPaymentTransitionException>(() => payment.Cancel());
    }

    [Fact]
    public void Refund_FromSettled_ShouldAddRefundEntity()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();
        payment.Settle();

        var refundAmount = new Money(50m, Currency.GBP);
        var refund = payment.Refund(refundAmount, "Customer request");

        Assert.NotNull(refund);
        Assert.Single(payment.Refunds);
        Assert.Equal(refundAmount.Amount, payment.Refunds.First().Amount);
        Assert.Contains(payment.DomainEvents, e => e is RefundCreated);
    }

    [Fact]
    public void Refund_FromSettled_ExceedsAmount_ShouldThrow()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();
        payment.Settle();

        var refundAmount = new Money(150m, Currency.GBP);

        Assert.Throws<InsufficientRefundableAmountException>(() => payment.Refund(refundAmount, "Customer request"));
    }

    [Fact]
    public void Refund_MultiplePartialRefunds_ShouldSucceed()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();
        payment.Settle();

        payment.Refund(new Money(30m, Currency.GBP), "First refund");
        payment.Refund(new Money(30m, Currency.GBP), "Second refund");

        Assert.Equal(40m, payment.RefundableAmount.Amount);
    }

    [Fact]
    public void Refund_FromCreated_ShouldThrow()
    {
        var payment = CreateTestPayment();

        Assert.Throws<InvalidPaymentTransitionException>(() => 
            payment.Refund(new Money(50m, Currency.GBP), "Customer request"));
    }

    [Fact]
    public void RefundableAmount_AfterFailedRefund_ShouldNotReduceBalance()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");
        payment.Capture();
        payment.Settle();

        var refund = payment.Refund(new Money(50m, Currency.GBP), "Customer request");
        refund.MarkFailed("Gateway error");

        Assert.Equal(100m, payment.RefundableAmount.Amount);
    }

    [Fact]
    public void DomainEvents_ClearedAfterApplying()
    {
        var payment = CreateTestPayment();
        payment.Authorise("gw_123");

        Assert.NotEmpty(payment.DomainEvents);

        payment.ApplyDomainEvents();

        Assert.Empty(payment.DomainEvents);
    }

    private Payment CreateTestPayment()
    {
        var amount = new Money(100m, Currency.GBP);
        var paymentMethod = new PaymentMethodSnapshot("card", "4242", "visa");

        return Payment.Create(
            _tenantId,
            _idempotencyKey,
            amount,
            Currency.GBP,
            PaymentMode.Live,
            _customerId,
            paymentMethod);
    }
}
