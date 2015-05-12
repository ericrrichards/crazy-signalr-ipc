using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Owin.Hosting;

namespace SignalR_Ipc {
    public class Server {
        private readonly List<Process> _clients;
        private readonly int _numClients;

        public Server(int numClients) {
            _numClients = numClients;
            _clients = new List<Process>(numClients);
        }

        public void Run() {
            using (WebApp.Start<Startup>("http://+:8080/")) {
                var hubConnection = new HubConnection("http://localhost:8080/");
                var proxy = hubConnection.CreateHubProxy("IpcHub");
                proxy.On("ShutDown", () => Console.WriteLine("Shutdown order received"));
                hubConnection.Start().Wait();
                
                for (var i = 0; i < _numClients; i++) {
                    var startInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().ManifestModule.Name) {Arguments = i.ToString()};
                    var p1 = new Process {StartInfo = startInfo};
                    Console.WriteLine("Starting client " + i);
                    p1.Start();
                    _clients.Add(p1);
                }

                Thread.Sleep(5000);

                for (var i = 0; i < _numClients; i++) {
                    Console.WriteLine("Getting Name from client " + i);
                    Console.WriteLine(proxy.Invoke<string>("GetName", i).Result);
                }

                Console.WriteLine("Press enter to shutdown...");
                Console.ReadLine();
                proxy.Invoke("ShutDown");
                while (_clients.Any(p => !p.HasExited)) {
                    Thread.Sleep(100);
                }
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
            }
        }
    }
}