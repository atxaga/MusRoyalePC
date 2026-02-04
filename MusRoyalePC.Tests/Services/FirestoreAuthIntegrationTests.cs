using MusRoyalePC.Services;
using System.Threading.Tasks;
using Xunit;

namespace MusRoyalePC.Tests.Services
{
    public class FirestoreAuthIntegrationTests
    {
        [Fact, Trait("Type", "Integration")]
        public async Task Login_ValidUser_ReturnsSuccess()
        {
            var auth = new FirestoreAuthService();
            var result = await auth.LoginAsync("bittortelletxea@gmail.com", "bittor@gmail.com");
            Assert.True(result.Success);
            Assert.NotNull(result.UserId);
            Assert.NotNull(result.UserName);
        }

        [Fact, Trait("Type", "Integration")]
        public async Task Login_WrongPassword_ReturnsFail()
        {
            var auth = new FirestoreAuthService();
            var result = await auth.LoginAsync("bittortelletxea@gmail.com", "asassas");
            Assert.False(result.Success);
            Assert.Equal("Pasahitza okerra", result.ErrorMessage);
        }

        [Fact, Trait("Type", "Integration")]
        public async Task Login_UserNotExist_ReturnsFail()
        {
            var auth = new FirestoreAuthService();
            var result = await auth.LoginAsync("usuarioinexistente@gmail.com", "whatever");
            Assert.False(result.Success);
            Assert.Equal("Erabiltzailea ez da existitzen", result.ErrorMessage);
        }
    }
}
