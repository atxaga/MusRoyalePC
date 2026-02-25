using MusRoyalePC;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MusRoyalePC.ViewModels
{
    public class BikoteakViewModel : INotifyPropertyChanged
    {
        private int _reyesSeleccionados = 4;
        private string _betAmount = "0";
        private bool _isFriendsPopupOpen;

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

        // Cambiado a 'Amigos' para ser consistente
        public ObservableCollection<string> Amigos { get; set; } = new ObservableCollection<string>
        { "Pello", "Miren", "Gorka", "Ane" };

        // COMANDOS
        public ICommand ToggleFriendsCommand => new RelayCommand(_ => IsFriendsPopupOpen = !IsFriendsPopupOpen);

        public ICommand SelectReyesCommand => new RelayCommand(p => {
            if (p != null) ReyesSeleccionados = int.Parse(p.ToString());
        });

        public ICommand InviteFriendCommand => new RelayCommand(amigo => {
            // Lógica para invitar
            IsFriendsPopupOpen = false;
        });

        public ICommand StartMatchCommand => new RelayCommand(_ => {
            // Lógica para iniciar partida
        });

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
