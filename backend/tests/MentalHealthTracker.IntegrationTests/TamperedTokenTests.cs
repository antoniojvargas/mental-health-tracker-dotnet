using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// Las pruebas "con cookie manipulada" necesitan un token que de verdad deje de verificar.
// Cambiar el ÚLTIMO carácter de la firma no lo garantiza: una firma HS256 son 32 bytes, que
// en base64url son 43 caracteres, y el último solo aporta 4 de sus 6 bits al valor (los otros
// 2 son relleno que se ignora), así que ~1 de cada 16 cambios deja los mismos bytes y el
// token sigue siendo válido (la causa del flaky de Me_/Negotiate_WithTamperedCookie). Desde
// que JwtService decodifica con Base64Url de .NET 9 (estricto) eso ya no ocurre, pero el
// helper cambia el primer carácter, que altera los bytes pase lo que pase.
[Collection(IntegrationTestGroup.Name)]
public sealed class TamperedTokenTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private const int Samples = 500;

    [Fact]
    public void TamperSignature_AlwaysProducesATokenThatDoesNotVerify()
    {
        using var scope = Factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>();

        for (var i = 0; i < Samples; i++)
        {
            var token = jwt.Sign(Guid.NewGuid(), $"user{i}@example.com");
            Assert.NotNull(jwt.Verify(token));

            var tampered = TamperSignature(token);

            Assert.NotEqual(token, tampered);
            Assert.Null(jwt.Verify(tampered));
        }
    }

    [Fact]
    public void ChangingTheLastCharacter_NeverVerifies()
    {
        // Con el base64url manual anterior, ~1 de cada 16 de estos cambios decodificaba a los
        // mismos bytes y el token seguía siendo válido (firma maleable). Base64Url de .NET 9 solo
        // acepta la codificación canónica, así que ahora ninguno verifica. TamperSignature se
        // mantiene porque el cambio del primer carácter altera los bytes con independencia del
        // decodificador.
        using var scope = Factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>();

        for (var i = 0; i < Samples; i++)
        {
            var token = jwt.Sign(Guid.NewGuid(), $"user{i}@example.com");
            var lastCharChanged = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

            Assert.Null(jwt.Verify(lastCharChanged));
        }
    }
}
