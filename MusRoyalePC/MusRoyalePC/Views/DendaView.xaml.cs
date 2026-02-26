using MusRoyalePC.Models;
using MusRoyalePC.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MusRoyalePC.Views;

public partial class DendaView : UserControl
{
    public ObservableCollection<AvatarShopItemVm> Items { get; } = new();

    private int _oro;
    public int Oro
    {
        get => _oro;
        set { _oro = value; }
    }

    private string _currentAvatar = "avadef.png";
    private string _userId = string.Empty;

    public DendaView()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += async (_, __) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _userId = FirestoreService.Instance.CurrentUserId;
            if (string.IsNullOrWhiteSpace(_userId))
                _userId = Properties.Settings.Default.savedId;

            var svc = new AvatarStoreService(FirestoreService.Instance.Db);
            var catalog = await svc.GetCatalogAsync();
            var (oro, owned) = await svc.GetUserOroAndOwnedAsync(_userId);

            var userDoc = await FirestoreService.Instance.Db.Collection("Users").Document(_userId).GetSnapshotAsync();
            if (userDoc.Exists && userDoc.ContainsField("avatarActual"))
                _currentAvatar = userDoc.GetValue<string>("avatarActual") ?? "avadef.png";

            Oro = oro;

            Items.Clear();
            foreach (var a in catalog)
            {
                var vm = new AvatarShopItemVm(a.File, a.Name, a.PriceOro)
                {
                    IsOwned = owned.Contains(a.File),
                    IsCurrent = string.Equals(_currentAvatar, a.File, StringComparison.OrdinalIgnoreCase)
                };
                Items.Add(vm);
            }

            // Asegurar que el default aparezca aunque no esté en catálogo
            if (!Items.Any(i => i.File.Equals("avadef.png", StringComparison.OrdinalIgnoreCase)))
            {
                Items.Insert(0, new AvatarShopItemVm("avadef.png", "Default", 0)
                {
                    IsOwned = true,
                    IsCurrent = string.Equals(_currentAvatar, "avadef.png", StringComparison.OrdinalIgnoreCase)
                });
            }

            // refresh binding
            Dispatcher.Invoke(() =>
            {
                // force update of Oro binding
                DataContext = null;
                DataContext = this;
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Errorea denda kargatzean: " + ex.Message);
        }
    }

    private async void AvatarAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not AvatarShopItemVm item) return;

        try
        {
            var svc = new AvatarStoreService(FirestoreService.Instance.Db);

            if (!item.IsOwned)
            {
                await svc.PurchaseAvatarAsync(_userId, new AvatarStoreItem { File = item.File, Name = item.Name, PriceOro = item.PriceOro });
            }

            // Set as current
            await svc.SetCurrentAvatarAsync(_userId, item.File);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}
