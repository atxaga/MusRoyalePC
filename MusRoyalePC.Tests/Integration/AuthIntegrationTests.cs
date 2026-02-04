using MusRoyalePC.Services;
using Xunit;
using System.Threading.Tasks;

namespace MusRoyalePC.Tests.Integration
{
    public class AuthIntegrationTests
    {
        private readonly IAuthService authService;

        public AuthIntegrationTests()
        {
            authService = new FirestoreAuthService();
        }

        [Fact, Trait("Type", "Integration")]
        public async Task AdminUser_ShouldBeRecognizedAsAdmin()
        {
            var result = await authService.LoginAsync("bittortelletxea@gmail.com", "bittor@gmail.com");

            Assert.True(result.Success, "Login debe ser exitoso");
            Assert.Equal("bittor", result.UserName);
            Assert.Equal("1", result.Role);
        }

        [Fact, Trait("Type", "Integration")]
        public async Task NormalUser_ShouldNotBeAdmin()
        {
            var result = await authService.LoginAsync("iker@gmail.com", "123456");
            Assert.True(result.Success, "Login debe ser exitoso");
            Assert.Equal("0", result.Role);
        }

        [Fact, Trait("Type", "Integration")]
        public async Task WrongPassword_ShouldFail()
        {
            var result = await authService.LoginAsync("bittortelletxea@gmail.com", "wrongpassword");
            Assert.False(result.Success);
            Assert.Equal("Pasahitza okerra", result.ErrorMessage);
        }

        [Fact, Trait("Type", "Integration")]
        public async Task NonExistentUser_ShouldFail()
        {
            var result = await authService.LoginAsync("usuarioinexistente@gmail.com", "whatever");
            Assert.False(result.Success);
            Assert.Equal("Erabiltzailea ez da existitzen", result.ErrorMessage);
        }
    }
}
