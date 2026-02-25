using MusRoyalePC.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusRoyalePC.Views.Controls;

public partial class DuoInviteToast : UserControl
{
    public DuoInviteToastVm Vm { get; }

    public DuoInviteToast(DuoInviteToastVm vm)
    {
        InitializeComponent();
        Vm = vm;
        DataContext = vm;
    }
}

public sealed class DuoInviteToastVm
{
    private readonly DuoInviteCoordinator _coord;

    public DuoInviteUi InviteUi { get; }

    public string EmisorName => InviteUi.EmisorName;
    public string EmisorAvatarUri => InviteUi.EmisorAvatarUri;
    public string Message => InviteUi.Message;

    public ICommand AcceptCommand { get; }
    public ICommand RejectCommand { get; }

    public event Action? RequestClose;

    public DuoInviteToastVm(DuoInviteCoordinator coord, DuoInviteUi ui)
    {
        _coord = coord;
        InviteUi = ui;

        AcceptCommand = new RelayCommand(async _ =>
        {
            await _coord.AcceptInviteAsync(ui.Invite);
            RequestClose?.Invoke();
        });

        RejectCommand = new RelayCommand(async _ =>
        {
            await _coord.RejectInviteAsync(ui.Invite);
            RequestClose?.Invoke();
        });
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        public RelayCommand(Func<object?, Task> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public async void Execute(object? parameter) => await _execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
