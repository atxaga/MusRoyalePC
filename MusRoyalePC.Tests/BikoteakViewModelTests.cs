using MusRoyalePC.ViewModels;
using Xunit;

namespace MusRoyalePC.Tests;

public class BikoteakViewModelTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var vm = new BikoteakViewModel();

        Assert.Equal(4, vm.ReyesSeleccionados);
        Assert.Equal("0", vm.BetAmount);
        Assert.False(vm.IsFriendsPopupOpen);

        // Hasierako lagun zerrenda "seed"-a badagoela (repoan 4 izen daude)
        Assert.NotNull(vm.Amigos);
        Assert.NotEmpty(vm.Amigos);
    }

    [Fact]
    public void ToggleFriendsCommand_TogglesIsFriendsPopupOpen()
    {
        var vm = new BikoteakViewModel();

        Assert.False(vm.IsFriendsPopupOpen);

        vm.ToggleFriendsCommand.Execute(null);
        Assert.True(vm.IsFriendsPopupOpen);

        vm.ToggleFriendsCommand.Execute(null);
        Assert.False(vm.IsFriendsPopupOpen);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    [InlineData("8", 8)]
    public void SelectReyesCommand_WhenParameterIsStringNumber_SetsReyesSeleccionados(string input, int expected)
    {
        var vm = new BikoteakViewModel();

        vm.SelectReyesCommand.Execute(input);

        Assert.Equal(expected, vm.ReyesSeleccionados);
    }

    [Fact]
    public void SelectReyesCommand_WhenParameterIsNull_DoesNotChangeReyesSeleccionados()
    {
        var vm = new BikoteakViewModel();
        var before = vm.ReyesSeleccionados;

        vm.SelectReyesCommand.Execute(null);

        Assert.Equal(before, vm.ReyesSeleccionados);
    }

    [Fact]
    public void InviteFriendCommand_ClosesPopup()
    {
        var vm = new BikoteakViewModel();

        // ireki popup-a
        vm.ToggleFriendsCommand.Execute(null);
        Assert.True(vm.IsFriendsPopupOpen);

        // gonbidapena "bidali" (komandoak popup-a ixtea egiten du)
        vm.InviteFriendCommand.Execute("Pello");

        Assert.False(vm.IsFriendsPopupOpen);
    }

    [Fact]
    public void Commands_AreNotNull()
    {
        var vm = new BikoteakViewModel();

        Assert.NotNull(vm.ToggleFriendsCommand);
        Assert.NotNull(vm.SelectReyesCommand);
        Assert.NotNull(vm.InviteFriendCommand);
        Assert.NotNull(vm.StartMatchCommand);
    }
}