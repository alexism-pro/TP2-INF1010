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
            TbDiscussion.AppendText("Vous avez joint la discussion.\n");

            //Création d'un thread écoutant le serveur
            this._client = client;
            _listener = new Thread(() => ServerListener());
            _listener.Start();
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                btnSendMessage_Click(sender, e);
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (TbMessage.Text.ToLower() == "list")
            {
                SendPacketToServer("LIST");
                TbMessage.Text = "";
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
                TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Aucun destinataire n'est sélectionné.\n", Brushes.Purple);
                return;
            }

            SendPacketToServer("MSG$#END#$" + list.Remove(list.Length - 1) + "$#END#$" + TbMessage.Text);
            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "À " + displayList.Remove(displayList.Length - 2, 2) + " : ", Brushes.Red);
            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), TbMessage.Text + "\n", Brushes.Black);
            TbMessage.Text = "";
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
                            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "De " + elements[1] + " : ", Brushes.Blue);
                            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), elements[2] + "\n", Brushes.Black);
                            break;

                        // Lorsqu'un utilisateur se connecte
                        case "CONN":
                            lock (_lockObject)
                                Items.Add(elements[1]);
                            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Connexion de " + elements[1] + "\n", Brushes.Green);
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
                            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Utilisateurs connectés :  " + elements[1] + "\n", Brushes.Green);
                            break;

                        // Lorsqu'un autre utilisateur se déconnecte
                        case "LOGOUT":
                            lock (_lockObject)
                                Items.Remove(elements[1]);
                            TbMessage.Dispatcher.Invoke(new UpdateTextCallback(UpdateText), "Déconnexion de " + elements[1] + "\n", Brushes.Green);
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

        public delegate void ServerDownCall();

        public delegate void UpdateTextCallback(string message, SolidColorBrush color);

        private void ServerIsDown()
        {
            MessageBox.Show("Le serveur a été fermé.");
            Close();
        }

        private void UpdateText(string message, SolidColorBrush color)
        {
            TextRange tr = new TextRange(TbDiscussion.Document.ContentEnd, TbDiscussion.Document.ContentEnd);
            tr.Text = message;
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
        }

        private void SendPacketToServer(string message)
        {
            StreamWriter sw = new StreamWriter(_client.GetStream());
            sw.WriteLine(message);
            sw.Flush();
        }

        #region XAML methods
        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            UsersList.SelectAll();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

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
