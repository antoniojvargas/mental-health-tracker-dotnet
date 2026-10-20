using System.Reflection;
using MentalHealthTracker.Api.Core.Errors;
using MentalHealthTracker.Api.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MentalHealthTracker.UnitTests;

public sealed class ProducesStandardErrorsAttributeTests
{
    private static ActionModel ActionWith(string methodName, params object[] attributes) =>
        new(typeof(Sample).GetMethod(methodName)!, attributes);

    private static List<ProducesResponseTypeAttribute> Responses(ActionModel action) =>
        action.Filters.OfType<ProducesResponseTypeAttribute>().ToList();

    [Fact]
    public void Apply_AddsTheFourStandardErrorsWithTheErrorEnvelope()
    {
        var action = ActionWith(nameof(Sample.Plain));

        new ProducesStandardErrorsAttribute().Apply(action);

        var responses = Responses(action);
        Assert.Equal([400, 401, 429, 500], responses.Select(response => response.StatusCode).Order());
        Assert.All(responses, response => Assert.Equal(typeof(ErrorResponse), response.Type));
    }

    [Fact]
    public void Apply_WithNotFound_AddsThe404Too()
    {
        var action = ActionWith(nameof(Sample.Plain));

        new ProducesStandardErrorsAttribute { NotFound = true }.Apply(action);

        Assert.Equal([400, 401, 404, 429, 500], Responses(action).Select(response => response.StatusCode).Order());
    }

    [Fact]
    public void Apply_DoesNotDuplicateACodeTheActionAlreadyDeclares()
    {
        var action = ActionWith(nameof(Sample.Plain));
        action.Filters.Add(new ProducesResponseTypeAttribute(typeof(string), 400));

        new ProducesStandardErrorsAttribute().Apply(action);

        var responses = Responses(action);
        Assert.Equal(4, responses.Count);
        var bad = Assert.Single(responses, response => response.StatusCode == 400);
        Assert.Equal(typeof(string), bad.Type);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        var action = ActionWith(nameof(Sample.Plain));
        var attribute = new ProducesStandardErrorsAttribute();

        attribute.Apply(action);
        attribute.Apply(action);

        Assert.Equal(4, Responses(action).Count);
    }

    [Fact]
    public void TheAttributeIsPickedUpAsAnActionConvention()
    {
        var method = typeof(Sample).GetMethod(nameof(Sample.Annotated))!;

        Assert.NotNull(method.GetCustomAttribute<ProducesStandardErrorsAttribute>());
        Assert.IsAssignableFrom<IActionModelConvention>(method.GetCustomAttribute<ProducesStandardErrorsAttribute>());
    }

    private sealed class Sample
    {
        public static void Plain()
        {
        }

        [ProducesStandardErrors]
        public static void Annotated()
        {
        }
    }
}
