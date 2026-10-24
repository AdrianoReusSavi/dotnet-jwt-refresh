namespace JwtRefresh.Application.Exceptions;

public class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string message) : base(message) { }
    public UniqueConstraintViolationException(string message, Exception innerException) : base(message, innerException) { }
}