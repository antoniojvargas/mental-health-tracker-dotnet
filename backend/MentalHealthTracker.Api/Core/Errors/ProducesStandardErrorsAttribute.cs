using MentalHealthTracker.Api.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MentalHealthTracker.Api.Core.Errors;

// Documenta en OpenAPI los errores que cualquier acción autenticada de la API puede devolver
// con el sobre de error estándar (ErrorResponse): 400 (validación), 401, 429 (rate limiting) y
// 500; con NotFound = true, también 404. Es una convención de acción: MVC la aplica al
// construir el modelo y añade los ProducesResponseType a la acción, en lugar de repetir los
// cuatro o cinco atributos en cada método. Si la acción ya declara un código, no lo duplica.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProducesStandardErrorsAttribute : Attribute, IActionModelConvention
{
    // Añade también el 404 (la acción puede responder "no existe" con el sobre NOT_FOUND).
    public bool NotFound { get; init; }

    public void Apply(ActionModel action)
    {
        var declared = action.Filters
            .OfType<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode)
            .ToHashSet();

        foreach (var statusCode in StatusCodes())
        {
            if (declared.Add(statusCode))
            {
                action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ErrorResponse), statusCode));
            }
        }
    }

    internal IEnumerable<int> StatusCodes()
    {
        yield return Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest;
        yield return Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;

        if (NotFound)
        {
            yield return Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound;
        }

        yield return Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;
        yield return Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError;
    }
}
