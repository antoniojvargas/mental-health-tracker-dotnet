using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class RequireAuthSecurityOperationFilter : IOperationFilter
{
    private const string SchemeName = JwtService.SessionCookieName;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!IsAuthProtected(context))
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
            });
    }

    private static bool IsAuthProtected(OperationFilterContext context)
    {
        var method = context.MethodInfo;
        return method is not null &&
               (method.IsDefined(typeof(RequireAuthAttribute), inherit: true) ||
                method.DeclaringType?.IsDefined(typeof(RequireAuthAttribute), inherit: true) == true);
    }
}