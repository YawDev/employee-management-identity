namespace employee.management.identity.core.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException()
        {
        }
        public UnauthorizedException(string message)
            : base(message)
        {
        }
    }
}
