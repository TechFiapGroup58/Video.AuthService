namespace AuthService.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class UserAlreadyExistsException : DomainException
{
    public UserAlreadyExistsException(string email)
        : base($"Usuário com e-mail '{email}' já existe.") { }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("E-mail ou senha inválidos.") { }
}
