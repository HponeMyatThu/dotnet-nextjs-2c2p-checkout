namespace PaymentIntegration.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class CustomerNotFoundException : DomainException
{
    public CustomerNotFoundException(int customerId) 
        : base($"Customer with ID {customerId} was not found.") { }
}

public class ProductNotFoundException : DomainException
{
    public ProductNotFoundException(int productId) 
        : base($"Product with ID {productId} was not found.") { }
}

public class CustomerCardNotFoundException : DomainException
{
    public CustomerCardNotFoundException(int cardId, int customerId) 
        : base($"Customer card with ID {cardId} was not found or does not belong to customer {customerId}.") { }
}

public class TransactionNotFoundException : DomainException
{
    public TransactionNotFoundException(string invoiceNo) 
        : base($"Transaction with invoice number '{invoiceNo}' was not found.") { }
}

public class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException(string idempotencyKey) 
        : base($"A transaction with idempotency key '{idempotencyKey}' is currently in progress. Please retry shortly.") { }
}

public class InvalidPaymentOperationException : DomainException
{
    public InvalidPaymentOperationException(string message) : base(message) { }
}
