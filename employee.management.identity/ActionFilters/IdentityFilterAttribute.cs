using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace employee.management.identity.ActionFilters
{
    public class IdentityFilterAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get the identityId parameter from the request
            if (!context.ActionArguments.TryGetValue("id", out var identityIdObj) ||
                identityIdObj is not Guid identityId)
            {
                throw new BadRequestException("id parameter is required and must be a valid Guid.");
            }

            // Get the currently signed-in user's ID from claims
            var signedInUserIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(signedInUserIdClaim) || !Guid.TryParse(signedInUserIdClaim, out Guid signedInUserId))
            {
                throw new UnauthorizedException("No authenticated user found.");
            }

            // Resolve the User Identity service from DI
            var userIdentityService = context.HttpContext.RequestServices
                .GetRequiredService<IUserIdentityService>();

            var identityUser = await userIdentityService.GetIdentityUserInfo(identityId) ?? throw new UnauthorizedException("Identity user not found.");

            // Compares it against the identity user being requested
            if (identityUser.Id != signedInUserId)
            {
                throw new UnauthorizedException("Identity is not authorized to access requested information.");
            }

            // Store validated user in HttpContext.Items for reuse in the action
            context.HttpContext.Items["AuthenticatedIdentity"] = identityUser;

            await next();
        }
    }
}
