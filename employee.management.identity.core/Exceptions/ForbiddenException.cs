namespace employee.management.identity.core.Exceptions
{
    public class ForbiddenException : Exception
    {
        public ForbiddenException()
        {
        }
        public ForbiddenException(string message)
            : base(message)
        {
        }
    }
}
