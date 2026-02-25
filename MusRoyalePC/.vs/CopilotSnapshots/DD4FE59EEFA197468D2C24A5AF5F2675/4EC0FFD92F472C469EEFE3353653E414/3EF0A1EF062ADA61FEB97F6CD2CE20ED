using Google.Cloud.Firestore;
using MusRoyalePC.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MusRoyalePC.ViewModels
{
    public class BikoteakViewModel : INotifyPropertyChanged
    {
        private int _reyesSeleccionados = 4;
        private string _betAmount = "0";
        private bool _isFriendsPopupOpen;
        private FriendPick? _selectedFriend;
        private readonly DuoMatchmakingService _duoService = new();
        private string? _activePartidaDuoId;
        private string _statusText = string.Empty;
        private bool _canPlay;
        private bool _canInvite = true;

        public int ReyesSeleccionados
        {
            get => _reyesSeleccionados;
            set { _reyesSeleccionados = value; OnPropertyChanged(); }
        }

        public bool IsFriendsPopupOpen
        {
            get => _isFriendsPopupOpen;
            set { _isFriendsPopupOpen = value; OnPropertyChanged(); }
        }

        public string BetAmount
        {
            get => _betAmount;
            set { _betAmount = value; OnPropertyChanged(); }
        }

        public FriendPick? SelectedFriend
        {
            get => _selectedFriend;
            set { _selectedFriend = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFriendDisplay)); }
        }

        public string SelectedFriendDisplay => SelectedFriend == null ? "" : $"Gonbidatua: {SelectedFriend.Name}";

        public ObservableCollection<FriendPick> Amigos { get; } = new();

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public bool CanPlay
        {
            get => _canPlay;
            private set { _canPlay = value; OnPropertyChanged(); }
        }

        public bool CanInvite
        {
            get => _canInvite;
            private set { _canInvite = value; OnPropertyChanged(); }
        }

        // COMANDOS
        public ICommand ToggleFriendsCommand => new RelayCommand(_ =>
        {
            IsFriendsPopupOpen = !IsFriendsPopupOpen;
            if (IsFriendsPopupOpen && Amigos.Count == 0)
                _ = LoadFriendsAsync();
        });

        public ICommand InviteFriendCommand => new RelayCommand(async amigo =>
        {
            if (amigo is not FriendPick f)
                return;

            SelectedFriend = f;
            IsFriendsPopupOpen = false;

            // Crear invitación en Firestore (PartidaDuo)
            var me = FirestoreService.Instance.CurrentUserId;
            if (string.IsNullOrWhiteSpace(me))
                me = Models.UserSession.Instance.DocumentId;

            if (string.IsNullOrWhiteSpace(me))
                return;

            int apuesta = int.TryParse(BetAmount, out var a) ? a : 0;

            try
            {
                CanInvite = false;
                CanPlay = false;
                StatusText = "Gonbidapena bidalita...";

                _activePartidaDuoId = await _duoService.CreateInviteAsync(me, f.Id, apuesta);
                _duoService.OnStateChanged -= OnDuoStateChanged;
                _duoService.OnStateChanged += OnDuoStateChanged;
                _duoService.Listen(_activePartidaDuoId);
            }
            catch (Exception ex)
            {
                CanInvite = true;
                StatusText = "Errorea gonbidapena bidaltzean.";
                MessageBox.Show(ex.Message);
            }
        });

        public ICommand StartMatchCommand => new RelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(_activePartidaDuoId))
            {
                MessageBox.Show("Lehenengo lagun bat gonbidatu.");
                return;
            }

            try
            {
                await _duoService.SetPlayAsync(_activePartidaDuoId);

                // Emisor también entra a la partida
                await StartDuoMatchAsync(_activePartidaDuoId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        });

        private static async Task StartDuoMatchAsync(string partidaId)
        {
            if (Application.Current.MainWindow is not MainWindow mw)
                return;

            if (mw.DataContext is not MainViewModel vm)
                return;

            string ipServer = "52.72.136.36";
            int puerto = 13000;

            vm.NetService.MiIdFirestore = Models.UserSession.Instance.DocumentId;
            string res = await vm.NetService.ConectarYUnirse(ipServer, puerto, "ID_ESKATU");
            if (res == "OK")
            {
                vm.Navegar("Partida");

                try
                {
                    await FirestoreService.Instance.Db.Collection("PartidaDuo").Document(partidaId).DeleteAsync();
                }
                catch { }
            }
            else
            {
                MessageBox.Show("Ezin izan da ID_ESKATU partidan sartu.");
            }
        }

        public ICommand CancelInviteCommand => new RelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(_activePartidaDuoId))
                return;

            try
            {
                await _duoService.DeleteAsync(_activePartidaDuoId);
            }
            catch { }

            ResetDuoUi();
        });

        public ICommand SelectReyesCommand => new RelayCommand(p =>
        {
            if (p != null) ReyesSeleccionados = int.Parse(p.ToString());
        });

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private async Task LoadFriendsAsync()
        {
            try
            {
                var db = FirestoreService.Instance.Db;
                string currentUserId = FirestoreService.Instance.CurrentUserId;
                if (string.IsNullOrWhiteSpace(currentUserId))
                    currentUserId = Models.UserSession.Instance.DocumentId;

                if (string.IsNullOrWhiteSpace(currentUserId))
                    return;

                DocumentSnapshot userDoc = await db.Collection("Users").Document(currentUserId).GetSnapshotAsync();
                if (!userDoc.Exists || !userDoc.ContainsField("amigos"))
                    return;

                var ids = userDoc.GetValue<System.Collections.Generic.List<string>>("amigos");
                Application.Current.Dispatcher.Invoke(() => Amigos.Clear());

                foreach (var id in ids)
                {
                    DocumentSnapshot fDoc = await db.Collection("Users").Document(id).GetSnapshotAsync();
                    if (!fDoc.Exists) continue;

                    string name = fDoc.ContainsField("username") ? fDoc.GetValue<string>("username") : id;
                    string avatar = fDoc.ContainsField("avatarActual") ? fDoc.GetValue<string>("avatarActual") : "avadef.png";
                    string avatarUri = $"pack://application:,,,/Assets/{avatar}";

                    var friend = new FriendPick(id, name, avatarUri);
                    Application.Current.Dispatcher.Invoke(() => Amigos.Add(friend));
                }
            }
            catch
            {
                // ignore
            }
        }

        private void OnDuoStateChanged(DuoMatchState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (state.Phase == DuoPhase.Deleted)
                {
                    ResetDuoUi();
                    return;
                }

                if (state.Onartua)
                {
                    StatusText = "Laguna prest dago jolasteko!";
                    CanPlay = true;
                }
                else
                {
                    StatusText = "Gonbidapena bidalita...";
                    CanPlay = false;
                }

                CanInvite = false;

                if (state.Onartua && state.Jokatu)
                {
                    StatusText = "Partida hasten...";
                }
            });
        }

        private void ResetDuoUi()
        {
            _duoService.StopListening();
            _activePartidaDuoId = null;
            SelectedFriend = null;
            CanInvite = true;
            CanPlay = false;
            StatusText = string.Empty;
        }
    }

    public sealed record FriendPick(string Id, string Name, string AvatarUri);
}
