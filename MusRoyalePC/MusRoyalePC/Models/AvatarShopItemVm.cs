using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusRoyalePC.Models;

public sealed class AvatarShopItemVm : INotifyPropertyChanged
{
    public string File { get; }
    public string Name { get; }
    public int PriceOro { get; }

    private bool _isOwned;
    public bool IsOwned
    {
        get => _isOwned;
        set { if (_isOwned != value) { _isOwned = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActionLabel)); } }
    }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { if (_isCurrent != value) { _isCurrent = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActionLabel)); } }
    }

    public string ActionLabel
    {
        get
        {
            if (!IsOwned) return $"EROSI ({PriceOro} ??)";
            if (IsCurrent) return "HAUTATUTA";
            return "HAUTATU";
        }
    }

    public AvatarShopItemVm(string file, string name, int priceOro)
    {
        File = file;
        Name = name;
        PriceOro = priceOro;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
