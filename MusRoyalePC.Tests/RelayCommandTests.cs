namespace MusRoyalePC.Tests;

using MusRoyalePC;
using Xunit;

public class RelayCommandTests
{
    // execute null bada, constructorrak exception bota behar du
    [Fact]
    public void Constructor_WhenExecuteIsNull_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));

    // canExecute eman ez bada, CanExecute beti true
    [Fact]
    public void CanExecute_WhenNoPredicate_ReturnsTrue()
    {
        var cmd = new RelayCommand(_ => { });
        Assert.True(cmd.CanExecute(null));
        Assert.True(cmd.CanExecute("anything"));
    }

    // CanExecute-k predicate-a errespetatzen du
    [Fact]
    public void CanExecute_UsesPredicate()
    {
        var cmd = new RelayCommand(_ => { }, p => p is int i && i > 0);
        Assert.False(cmd.CanExecute(null));
        Assert.False(cmd.CanExecute(-1));
        Assert.True(cmd.CanExecute(1));
    }

    // Execute-k parametroa action-era pasatzen du
    [Fact]
    public void Execute_PassesParameterToAction()
    {
        object? received = null;
        var cmd = new RelayCommand(p => received = p);

        cmd.Execute("hello");

        Assert.Equal("hello", received);
    }

    // RaiseCanExecuteChanged() -> CanExecuteChanged event-a jaurtitzen da (UI eguneratzeko)
    [Fact]
    public void RaiseCanExecuteChanged_RaisesEvent()
    {
        var cmd = new RelayCommand(_ => { });
        var raised = 0;
        cmd.CanExecuteChanged += (_, __) => raised++;

        cmd.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }
}