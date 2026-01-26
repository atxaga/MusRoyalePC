using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusRoyalePC.Models;
using MusRoyalePC.Views;

namespace MusRoyalePC.Controllers
{
    public sealed class MainController : INotifyPropertyChanged
    {
        private readonly User _user = new()
        {
            Name = "iker",
            IsOnline = true,
            Balance = 0m
        };

        private NavTab _selectedTab = NavTab.Home;
        private UserControl? _currentView;
        private decimal _betAmount = 10m;

        // Seat labels: Player1 (bottom) is always the local user, Player2 (top) is the teammate
        private string _player1Label;
        private string _player2Label = "Kidea"; // teammate
        private string _player3Label = "Aurkari 1";
        private string _player4Label = "Aurkari 2";

        public event PropertyChangedEventHandler? PropertyChanged;


        public MainController()
        {
            _player1Label = _user.Name; // local user

            IncrementBalanceCommand = new RelayCommand(_ => Balance += 1m);
            OpenSettingsCommand = new RelayCommand(_ => ShowInfo("Ezarpenak (implementatu)"));
            PowerOffCommand = new RelayCommand(_ => Application.Current.Shutdown());

            NavigateCommand = new RelayCommand(param =>
            {
                if (param is string s && Enum.TryParse<NavTab>(s, out var tab))
                {
                    SelectedTab = tab;

                    // Updated: route "Lagunak" tab to LagunakView
                    CurrentView = tab switch
                    {
                        NavTab.Lagunak => new LagunakView(),
                        _ => null
                    };
                }
            });

            GoBackCommand = new RelayCommand(_ => CurrentView = null);
            StartMatchCommand = new RelayCommand(_ => CurrentView = new PartidaView());

            SelectModeCommand = new RelayCommand(param =>
            {
                if (param is string mode)
                {
                    switch (mode)
                    {
                        case "PartidaAzkarra":
                            CurrentView = new PartidaAzkarraView();
                            break;
                        case "Bikoteak":
                            CurrentView = new BikoteakView();
                            break;
                        case "Pribatua":
                            CurrentView = new PribatuaView();
                            break;
                    }
                    
                    
                }
            });
        }

        public string Player1Label
        {
            get => _player1Label;
            set { if (_player1Label != value) { _player1Label = value; OnPropertyChanged(); } }
        }
        public string Player2Label
        {
            get => _player2Label;
            set { if (_player2Label != value) { _player2Label = value; OnPropertyChanged(); } }
        }
        public string Player3Label
        {
            get => _player3Label;
            set { if (_player3Label != value) { _player3Label = value; OnPropertyChanged(); } }
        }
        public string Player4Label
        {
            get => _player4Label;
            set { if (_player4Label != value) { _player4Label = value; OnPropertyChanged(); } }
        }

        public string UserName
        {
            get => _user.Name;
            set
            {
                if (_user.Name != value)
                {
                    _user.Name = value;
                    OnPropertyChanged();
                    // Keep Player1 label in sync with local user by default
                    Player1Label = _user.Name;
                }
            }
        }

        public bool IsOnline
        {
            get => _user.IsOnline;
            set
            {
                if (_user.IsOnline != value)
                {
                    _user.IsOnline = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal Balance
        {
            get => _user.Balance;
            set
            {
                if (_user.Balance != value)
                {
                    _user.Balance = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal BetAmount
        {
            get => _betAmount;
            set
            {
                if (_betAmount != value)
                {
                    _betAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        public NavTab SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                }
            }
        }

        public UserControl? CurrentView
        {
            get => _currentView;
            private set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public ICommand IncrementBalanceCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand PowerOffCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand SelectModeCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand StartMatchCommand { get; }

        private static void ShowInfo(string message)
        {
            MessageBox.Show(message, "MusRoyalePC", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
