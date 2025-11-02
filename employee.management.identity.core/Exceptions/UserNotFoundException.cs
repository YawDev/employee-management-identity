namespace employee.management.identity.core.Exceptions
{
    public class UserNotFoundException : BadRequestException
    {
        public UserNotFoundException()
        {
        }
        public UserNotFoundException(string message)
            : base(message)
        {
        }
    }
}
