using System;
using System.IO;
using System.Net.Sockets;
using System.Windows;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpClient _client;

        public MainWindow() => InitializeComponent();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
            // Connexion au serveur
            try
            {
                _client = new TcpClient("127.0.0.1", 8080);
            }
            catch (Exception)
            {
                LblError.Content = "Le serveur n'est pas ouvert."; 
                return;
            }

            var sw = new StreamWriter(_client.GetStream());
            sw.WriteLine("CONN$#END#$" + TbName.Text);
            sw.Flush();

            var sr = new StreamReader(_client.GetStream());
            var request = sr.ReadLine();

            if (request == "ACC")
            {
                LblError.Content = "";
                var chat = new Chat(TbName.Text, _client);
                Hide();
                chat.ShowDialog();
                Show();
            }
            else
            {
                LblError.Content = "Ce nom est déjà utilisé.";
            }
        }
    }
}