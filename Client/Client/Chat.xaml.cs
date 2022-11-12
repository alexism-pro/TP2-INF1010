using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;


namespace Client
{
    /// <summary>
    /// Interaction logic for Chat.xaml
    /// </summary>
    public partial class Chat : Window
    {
        private readonly TcpClient _client;
        private readonly Thread _listener;
        private readonly object _lockObject;
        private bool _serverCrashed;

        public ObservableCollection<string> Items { get; }

        public Chat(string username, TcpClient client)
        {
            _lockObject = new object();
            Items = new ObservableCollection<string>() { };
            BindingOperations.EnableCollectionSynchronization(Items, _lockObject);

            //Affichage interface
            DataContext = this;
            InitializeComponent();
            LblWelcome.Content = "Bienvenue " + username;
            TxtBoxDiscussion.AppendText("Nouvelle discussion...\n");

            //Création d'un thread écoutant le serveur
            _client = client;
            _listener = new Thread(() => ServerListener());
            _listener.Start();
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (TxtBoxMessage.Text.ToLower() == "list")
            {
                SendPacketToServer("LIST");
                TxtBoxMessage.Text = "";
                return;
            }

            string list = "", displayList = "";
            foreach (string dest in UsersList.SelectedItems)
            {
                list += dest + ":";
                displayList += dest + ", ";
            }

            if (list == "")
            {
                TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Aucun destinataire n'est sélectionné.\n", Brushes.Black);
                return;
            }

            SendPacketToServer("MSG$#END#$" + list.Remove(list.Length - 1) + "$#END#$" + TxtBoxMessage.Text);
            TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "À " + displayList.Remove(displayList.Length - 2, 2) + " : " + TxtBoxMessage.Text + "\n", Brushes.Red);
            TxtBoxMessage.Text = "";
        }

        private void ServerListener()
        {
            while (true)
            {
                var sr = new StreamReader(_client.GetStream());
                try
                {
                    var data = sr.ReadLine();
                    var elements = data.Split(new string[] {"$#END#$"}, StringSplitOptions.None);
                    
                    switch (elements[0])
                    {
                        // Lorsque l'on reçoit un message
                        case "MSG":
                            TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "De " + elements[1] + " : " + elements[2] + "\n", Brushes.Orange);
                            break;

                        // Lorsqu'un utilisateur se connecte
                        case "CONN":
                            lock (_lockObject)
                                Items.Add(elements[1]);
                            TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Connexion de " + elements[1] + "\n", Brushes.Red);
                            break;

                        // Lors de la connexion d'utilisateurs, pour les ajouter à notre liste d'utilisateurs disponibles
                        case "LIST":
                            var users = elements[1].Split(':');
                            lock (_lockObject)
                                foreach (var user in users)
                                    Items.Add(user);
                            break;

                        // Lorsque l'on reçoit la liste des utilisateurs connectés
                        case "LISTING":
                            TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Utilisateurs connectés :  " + elements[1] + "\n", Brushes.Red);
                            break;

                        // Lorsqu'un autre utilisateur se déconnecte
                        case "LOGOUT":
                            lock (_lockObject)
                                Items.Remove(elements[1]);
                            TxtBoxMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Déconnexion de " + elements[1] + "\n", Brushes.Red);
                            break;

                    }
                }
                // Si le serveur crash ou se ferme
                catch (IOException)
                {
                    Dispatcher.Invoke(ServerIsDown);
                    break;
                }
            }
        }

        private delegate void UpdateTextCallback(string message, SolidColorBrush color);

        private void ServerIsDown()
        {
            MessageBox.Show("Le serveur a été fermé.");
            Close();
        }

        private void UpdateText(string message, SolidColorBrush color)
        {
            var tr = new TextRange(TxtBoxDiscussion.Document.ContentEnd, TxtBoxDiscussion.Document.ContentEnd)
            {
                Text = message
            };
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
        }

        private void SendPacketToServer(string message)
        {
            var sw = new StreamWriter(_client.GetStream());
            sw.WriteLine(message);
            sw.Flush();
        }

        #region XAML methods
        private void btnSelectAll_Click(object sender, RoutedEventArgs e) => UsersList.SelectAll();

        private void btnLogout_Click(object sender, RoutedEventArgs e) => Close();

        // Libérer les ressources/listener
        private void ChatClosing(object sender, CancelEventArgs e)
        {
            if (_serverCrashed)
                return;

            _listener.Abort();
            
            SendPacketToServer("LOGOUT");
            _client.GetStream().Close();
            _client.Close();
        }
        #endregion
    }
}
