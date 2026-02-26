using Moq;
using Xunit;
using System.Threading.Tasks;
using MusRoyalePC.ViewModels;
using MusRoyalePC.Services;

namespace MusRoyalePC.Tests
{
    public class LoginTests
    {
        // 1. Sarrera hutsik: erabiltzailea hutsik
        [Fact]
        public async Task Login_UsuarioVacio_ErrorEremuHutsik()
        {
            var mockAuth = new Mock<IAuthService>();
            var vm = new LoginViewModel(mockAuth.Object) { Username = "", Password = "123" };
            var result = await vm.LoginCommand();
            Assert.False(result);
            Assert.Equal("Eremu guztiak bete", vm.ErrorMessage);
        }

        // 2. Sarrera hutsik: pasahitza hutsik
        [Fact]
        public async Task Login_PasswordVacio_ErrorEremuHutsik()
        {
            var mockAuth = new Mock<IAuthService>();
            var vm = new LoginViewModel(mockAuth.Object) { Username = "admin", Password = "" };
            var result = await vm.LoginCommand();
            Assert.False(result);
            Assert.Equal("Eremu guztiak bete", vm.ErrorMessage);
        }

        // 3. Okerreko kredentzialak (Mock devuelve false)
        [Fact]
        public async Task Login_KredentzialOkerrak_ErrorMezua()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            var vm = new LoginViewModel(mockAuth.Object) { Username = "bittor", Password = "wrong" };
            var result = await vm.LoginCommand();

            Assert.False(result);
            Assert.Equal("Kredentzial okerrak", vm.ErrorMessage);
            mockAuth.Verify(x => x.IsAdminAsync(It.IsAny<string>()), Times.Never); // No debe mirar si es admin si falla el pass
        }

        // 4. Erabiltzaile normala (Mock: true, Admin: false)
        [Fact]
        public async Task Login_UserNormal_Success_IsAdminFalse()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(x => x.ValidateUserAsync("iker", "123456")).ReturnsAsync(true);
            mockAuth.Setup(x => x.IsAdminAsync("iker")).ReturnsAsync(false);

            var vm = new LoginViewModel(mockAuth.Object) { Username = "iker", Password = "123456" };
            var result = await vm.LoginCommand();

            Assert.True(result);
            Assert.Equal("", vm.ErrorMessage);
        }

        // 5. Login admin (Mock: true, Admin: true)
        [Fact]
        public async Task Login_Admin_Success_IsAdminTrue()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(x => x.ValidateUserAsync("bittor", "bittor@gmail.com")).ReturnsAsync(true);
            mockAuth.Setup(x => x.IsAdminAsync("bittor")).ReturnsAsync(true);

            var vm = new LoginViewModel(mockAuth.Object) { Username = "bittor", Password = "bittor@gmail.com" };
            var result = await vm.LoginCommand();

            Assert.True(result);
        }

        // 6. Konexio salbuespena (Exception)
        [Fact]
        public async Task Login_Exception_ErrorKonexioa()
        {
            var mockAuth = new Mock<IAuthService>();
            mockAuth.Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new System.Exception());

            var vm = new LoginViewModel(mockAuth.Object) { Username = "a", Password = "b" };
            var result = await vm.LoginCommand();

            Assert.False(result);
            Assert.Equal("Konexio errorea", vm.ErrorMessage);
        }
    }
}