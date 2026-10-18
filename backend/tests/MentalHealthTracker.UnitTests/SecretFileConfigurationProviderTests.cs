using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace MentalHealthTracker.UnitTests;

// Colección sin paralelismo: estas pruebas leen y escriben variables de entorno, que son
// estado del proceso entero, y otra clase de pruebas que corriera a la vez las vería.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariablesGroup
{
    public const string Name = "Environment variables";
}

// Pruebas de caracterización de AddSecretFileConfiguration (el proveedor es privado, así
// que se ejerce construyendo una configuración real). Al empezar se vacían las seis
// variables que el proveedor lee, para que el entorno de quien ejecuta la suite no cuente,
// y Dispose deja cada una como estaba.
[Collection(EnvironmentVariablesGroup.Name)]
public sealed class SecretFileConfigurationProviderTests : IDisposable
{
    private static readonly string[] Variables =
    [
        "JWT_SECRET",
        "JWT_SECRET_FILE",
        "GOOGLE_CLIENT_SECRET",
        "GOOGLE_CLIENT_SECRET_FILE",
        "Database__ConnectionString",
        "Database__ConnectionString_FILE",
    ];

    private readonly Dictionary<string, string?> _original = [];

    private readonly List<string> _tempFiles = [];

    public SecretFileConfigurationProviderTests()
    {
        foreach (var variable in Variables)
        {
            _original[variable] = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    public void Dispose()
    {
        foreach (var (variable, value) in _original)
        {
            Environment.SetEnvironmentVariable(variable, value);
        }

        foreach (var file in _tempFiles)
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void PlainVariable_IsReadIntoItsConfigurationKey()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "plain-secret");

        Assert.Equal("plain-secret", Build()["Jwt:Secret"]);
    }

    [Fact]
    public void PlainVariable_WinsOverTheFileVariable()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "plain-secret");
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", WriteSecretFile("file-secret"));

        Assert.Equal("plain-secret", Build()["Jwt:Secret"]);
    }

    [Fact]
    public void FileVariable_IsReadFromTheFile()
    {
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET_FILE", WriteSecretFile("google-secret"));

        Assert.Equal("google-secret", Build()["GoogleOAuth:ClientSecret"]);
    }

    [Theory]
    [InlineData("secret\n")]
    [InlineData("secret\r\n")]
    [InlineData("  secret  ")]
    [InlineData("\n\n secret \t\n")]
    public void FileVariable_ContentIsTrimmed(string content)
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", WriteSecretFile(content));

        Assert.Equal("secret", Build()["Jwt:Secret"]);
    }

    [Fact]
    public void FileVariable_DoesNotTrimInsideTheValue()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", WriteSecretFile("a b\nc\n"));

        Assert.Equal("a b\nc", Build()["Jwt:Secret"]);
    }

    [Fact]
    public void WithNeitherVariable_TheKeyDoesNotExist()
    {
        var configuration = Build();

        Assert.Null(configuration["Jwt:Secret"]);
        Assert.Null(configuration["GoogleOAuth:ClientSecret"]);
        Assert.Null(configuration["Database:ConnectionString"]);
        Assert.False(configuration.GetSection("Jwt").Exists());
        Assert.False(configuration.GetSection("Database").Exists());
    }

    [Fact]
    public void EmptyPlainVariable_FallsBackToTheFile()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "");
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", WriteSecretFile("file-secret"));

        Assert.Equal("file-secret", Build()["Jwt:Secret"]);
    }

    [Fact]
    public void EmptyFile_YieldsAnEmptyValueNotAMissingKey()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", WriteSecretFile("  \n"));

        Assert.Equal(string.Empty, Build()["Jwt:Secret"]);
    }

    [Theory]
    [InlineData("JWT_SECRET_FILE")]
    [InlineData("GOOGLE_CLIENT_SECRET_FILE")]
    [InlineData("Database__ConnectionString_FILE")]
    public void FileVariablePointingToAMissingFile_ThrowsAnErrorNamingTheVariableAndThePath(string variable)
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt");
        Environment.SetEnvironmentVariable(variable, missing);

        var exception = Assert.Throws<InvalidOperationException>(Build);

        Assert.Contains(variable, exception.Message);
        Assert.Contains(missing, exception.Message);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void FileVariablePointingToAMissingDirectory_ThrowsTheSameClearError()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "secret.txt");
        Environment.SetEnvironmentVariable("JWT_SECRET_FILE", missing);

        var exception = Assert.Throws<InvalidOperationException>(Build);

        Assert.Contains("JWT_SECRET_FILE", exception.Message);
        Assert.Contains(missing, exception.Message);
        Assert.IsType<DirectoryNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void EachSecretMapsToItsOwnKey()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "jwt");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET_FILE", WriteSecretFile("google\n"));
        Environment.SetEnvironmentVariable("Database__ConnectionString_FILE", WriteSecretFile("Host=db\n"));

        var configuration = Build();

        Assert.Equal("jwt", configuration["Jwt:Secret"]);
        Assert.Equal("google", configuration["GoogleOAuth:ClientSecret"]);
        Assert.Equal("Host=db", configuration["Database:ConnectionString"]);
    }

    private static IConfigurationRoot Build() =>
        new ConfigurationBuilder().AddSecretFileConfiguration().Build();

    private string WriteSecretFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"secret-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}
