using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusRoyalePC.Views
{
    public partial class ChatView : UserControl
    {
        private FirestoreDb db;
        private string currentUserId;
        private string selectedFriendId;
        private FirestoreChangeListener chatListener;
        public ObservableCollection<ChatMessageUI> Messages { get; set; } = new ObservableCollection<ChatMessageUI>();

        public ChatView()
        {
            InitializeComponent();
            currentUserId = Properties.Settings.Default.savedId;
            db = Services.FirestoreService.Instance.Db;
            chatItems.ItemsSource = Messages;
            CargarAmigos();
        }

        private async void CargarAmigos()
        {
            // Similar a tu lógica de Kotlin para obtener la lista de amigos
            DocumentSnapshot userDoc = await db.Collection("Users").Document(currentUserId).GetSnapshotAsync();
            if (userDoc.Exists && userDoc.ContainsField("amigos"))
            {
                var amigosIds = userDoc.GetValue<List<string>>("amigos");
                var amigosLista = new List<FriendUI>();

                foreach (var id in amigosIds)
                {
                    DocumentSnapshot fDoc = await db.Collection("Users").Document(id).GetSnapshotAsync();
                    amigosLista.Add(new FriendUI
                    {
                        Id = id,
                        Name = fDoc.GetValue<string>("username"),
                        AvatarPath = "/Assets/" + (fDoc.GetValue<string>("avatarActual") ?? "avadef.png")
                    });
                }
                lstFriends.ItemsSource = amigosLista;
            }
        }

        private void lstFriends_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var friend = lstFriends.SelectedItem as FriendUI;
            if (friend == null) return;

            selectedFriendId = friend.Id;
            txtChatTitle.Text = friend.Name;
            EscucharChat(friend.Id);
        }

        private void EscucharChat(string friendId)
        {
            chatListener?.StopAsync(); // Detener listener previo
            Messages.Clear();

            Query query = db.Collection("Chats").OrderBy("timestamp");

            // Snapshot Listener en tiempo real
            chatListener = query.Listen(snapshot =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (DocumentChange change in snapshot.Changes)
                    {
                        if (change.ChangeType == DocumentChange.Type.Added)
                        {
                            var data = change.Document.ToDictionary();
                            string emisor = data["idemisor"].ToString();
                            string receptor = data["idreceptor"].ToString();

                            // Filtrar solo los mensajes de esta conversación
                            if ((emisor == currentUserId && receptor == friendId) ||
                                (emisor == friendId && receptor == currentUserId))
                            {
                                bool isMine = emisor == currentUserId;
                                Messages.Add(new ChatMessageUI
                                {
                                    Message = data["mensaje"].ToString(),
                                    Alignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                                    BubbleColor = isMine ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))
                                                         : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4E342E")),
                                    Time = DateTimeOffset.FromUnixTimeMilliseconds((long)data["timestamp"]).DateTime.ToShortTimeString()
                                });
                                scrollChat.ScrollToEnd();
                            }
                        }
                    }
                });
            });
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInput.Text) || selectedFriendId == null) return;

            Dictionary<string, object> msg = new Dictionary<string, object>
            {
                { "idemisor", currentUserId },
                { "idreceptor", selectedFriendId },
                { "mensaje", txtInput.Text },
                { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "leido", false }
            };

            await db.Collection("Chats").AddAsync(msg);
            txtInput.Clear();
        }

        private void btnToggleFriends_Click(object sender, RoutedEventArgs e)
        {
            FriendsColumn.Width = (FriendsColumn.Width.Value > 0) ? new GridLength(0) : new GridLength(250);
        }

        private void txtInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) btnSend_Click(null, null);
        }
    }

    // Clases auxiliares para el Binding
    public class ChatMessageUI
    {
        public string Message { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public SolidColorBrush BubbleColor { get; set; }
        public string Time { get; set; }
    }

    public class FriendUI
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AvatarPath { get; set; }
    }
}