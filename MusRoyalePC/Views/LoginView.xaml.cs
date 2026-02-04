using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using MusRoyalePC.Services;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MusRoyalePC.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            DataContext = new LoginViewModel(new FirestoreAuthService());
        }
    }
}