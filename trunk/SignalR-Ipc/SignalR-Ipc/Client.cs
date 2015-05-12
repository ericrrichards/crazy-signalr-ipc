using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNet.SignalR.Client;

namespace SignalR_Ipc {
    public class Client {
        private readonly string _name;
        private readonly int _clientId;

        public Client(int clientId) {
            _clientId = clientId;
            _name = "Client " + clientId;
        }

        public void Run() {
            var running = true;
            var queryStringData = new Dictionary<string, string> {
                {"id", _clientId.ToString()}
            };
            var hubConnection = new HubConnection("http://localhost:8080/", queryStringData);
            var proxy = hubConnection.CreateHubProxy("IpcHub");

            proxy.On("ShutDown", () => {
                Console.WriteLine("Shutdown order received");
                try {
                    hubConnection.Stop();
                } finally {
                    running = false;
                }
            });
            proxy.On("GetName", () => {
                Console.WriteLine("Name requested by server");
                proxy.Invoke("ReturnName", _clientId, _name);
            });

            hubConnection.Start().Wait();
            Console.WriteLine("Connected to server");

            while (running) {
                Thread.Sleep(1000);
            }
        }
    }
}