namespace employee.management.identity.core.Exceptions
{
    public class FailedAuthenticationException : BadRequestException
    {
        public FailedAuthenticationException()
        {
        }
        public FailedAuthenticationException(string message)
            : base(message)
        {
        }
    }
}
