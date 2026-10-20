using MentalHealthTracker.Domain.Errors;

namespace MentalHealthTracker.UnitTests;

// Los códigos van escritos a mano: son contrato con el cliente y renombrar una constante o
// cambiar su valor tiene que romper un test a la vista.
public sealed class ErrorCodesTests
{
    [Fact]
    public void TheCodesAreTheWireContract()
    {
        Assert.Equal("VALIDATION_ERROR", ErrorCodes.ValidationError);
        Assert.Equal("UNAUTHORIZED", ErrorCodes.Unauthorized);
        Assert.Equal("NOT_FOUND", ErrorCodes.NotFound);
        Assert.Equal("RATE_LIMITED", ErrorCodes.RateLimited);
        Assert.Equal("INTERNAL_ERROR", ErrorCodes.InternalError);
    }

    [Fact]
    public void ThereAreExactlyFiveCodes()
    {
        var constants = typeof(ErrorCodes).GetFields().Where(field => field.IsLiteral).ToList();

        Assert.Equal(5, constants.Count);
    }

    [Fact]
    public void TheDomainExceptionsCarryTheirCode()
    {
        Assert.Equal(ErrorCodes.NotFound, new NotFoundException("x").Code);
        Assert.Equal(ErrorCodes.Unauthorized, new UnauthorizedException("x").Code);
        Assert.Equal(ErrorCodes.ValidationError, new ValidationException("x").Code);
    }
}
