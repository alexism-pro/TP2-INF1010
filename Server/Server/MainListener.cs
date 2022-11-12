using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    class MainListener
    {
        private Dictionary<String, TcpClient> dictUsers = new Dictionary<string, TcpClient>();

        public MainListener() { }
        
        public void Start()
        {
            // Le serveur écoute sur l'adresse "127.0.0.1:8080"
            TcpListener listener = new(IPAddress.Parse("127.0.0.1"), 8080);
            listener.Start();
            
            // Initialisation du fichier de logs
            LogHelper.ClearFile();
            LogHelper.Log("Serveur démarré");

            // Listener principal qui gère les connexions et crée un thread(délègue à ServerListener(...)) pour chaque nouveau client
            while (true)
            {
                var client = listener.AcceptTcpClient();    // Appel bloquant (attend une demande de connexion)
                StreamReader sr = new(client.GetStream());
                StreamWriter sw = new(client.GetStream());
                
                try
                {
                    // Lit la demande sur le socket
                    var request = sr.ReadLine();
                    var elements = request.Split(new[] { "$#END#$" }, StringSplitOptions.None);

                    // Si la demande n'est pas bien formulée, on laisse tomber
                    if (elements[0] != "CONN")
                        continue;

                    if (!dictUsers.ContainsKey(elements[1]))
                    {
                        dictUsers.Add(elements[1], client);
                        new Thread(() => ClientListener(elements[1])).Start();
                        sw.WriteLine("ACC");
                        sw.Flush();
                        LogHelper.Log($"Accès autorisé à \"{elements[1]}\" à partir de l'IP: {IPAddress.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString())}");
                    }
                    else
                    {
                        sw.WriteLine("REF");
                        sw.Flush();
                        LogHelper.Log($"Accès refusé (nom d'utilisateur déjà utilisé) à \"{elements[1]}\" à partir de l'IP: {IPAddress.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString())}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    client.Close();
                }
            }
        }

        private void ClientListener(string username)
        {
            UsersList(username);
            
            var client = dictUsers[username];

            // Écoute le client
            while (true)
            {
                try
                {
                    var sr = new StreamReader(client.GetStream());
                    var request = sr.ReadLine();
                    var elements = request.Split(new[] {"$#END#$"}, StringSplitOptions.None);

                    switch (elements[0])
                    {
                        case "MSG":
                            var destinataires = elements[1].Split(':');
                            String sentTo = null;
                            foreach (var dest in destinataires)
                            {
                                if (sentTo != null)
                                    sentTo = sentTo + "," + dest;
                                else
                                    sentTo = dest;

                                if (dictUsers[dest] == null)
                                    continue;

                                if (dictUsers[dest].Connected)
                                {
                                    var sw = new StreamWriter(dictUsers[dest].GetStream());
                                    sw.WriteLine("MSG$#END#$" + username + "$#END#$" + elements[2]);
                                    sw.Flush();
                                }
                                else
                                    dictUsers.Remove(dest);
                            }

                            LogHelper.Log(
                                "Message envoyé à " + "\"" + sentTo + "\"" + " par " + "\"" + username + "\"..." +
                                "\n                     \t" + elements[2]
                                );
                            break;

                        case "LOGOUT":
                            foreach (var entry in dictUsers)
                            {
                                if (entry.Value.Connected)
                                {
                                    var sw = new StreamWriter(entry.Value.GetStream());
                                    sw.WriteLine("LOGOUT$#END#$" + username);
                                    sw.Flush();
                                }
                                else
                                    dictUsers.Remove(entry.Key);
                            }

                            LogHelper.Log("Fermeture de la communication avec " + username + " à partir de l'IP: " +
                                          IPAddress.Parse(((IPEndPoint) client.Client.RemoteEndPoint).Address.ToString()));
                            dictUsers.Remove(username);
                            username = null;
                            return;

                        case "LIST":
                            var list = 
                                dictUsers.Where(entry => entry.Key != username)
                                    .Aggregate("", (current, entry) => current + (entry.Key + ":"));
                            
                            if (list == "")
                                break;
                            
                            var sw2 = new StreamWriter(client.GetStream());
                            sw2.WriteLine("LISTING$#END#$" + list.Remove((list.Length - 1)));
                            sw2.Flush();
                            Console.WriteLine("Liste des utilisateurs envoyée à " + username);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e); return;
                }
            }
        }

        // Lors de la connexion, envoie au nouvel utilisateur la liste des utilisateurs déjà connectés
        private void UsersList(string username)
        {
            var list = 
                dictUsers.Where(entry => entry.Key != username)
                    .Aggregate("", (current, entry) => current + (entry.Key + ":"));

            if (list == "")
                return;
            
            foreach (var entry in dictUsers)
            {
                if (entry.Value.Connected)
                {
                    var sw = new StreamWriter(entry.Value.GetStream());
                    
                    if (entry.Key != username)
                        sw.WriteLine("CONN$#END#$" + username);
                    else
                        sw.WriteLine("LIST$#END#$" + list.Remove(list.Length - 1));
                    
                    sw.Flush();
                }
                else
                {
                    dictUsers.Remove(entry.Key);
                }
            }
        }
    }
}

