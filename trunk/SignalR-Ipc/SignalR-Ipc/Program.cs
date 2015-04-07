using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Owin.Hosting;
using Owin;

namespace SignalR_Ipc {
    static class Program {
        private static string _name;
        static void Main(string[] args) {

            if (args.Length == 0) {
                RunServer();
                
            } else {
                var clientId = Convert.ToInt32(args.FirstOrDefault());
                RunClient(clientId);

            }

        }

        private static void RunClient(int clientId) {
            _name = "Client " + clientId;

            var queryStringData = new Dictionary<string, string> {
                {"id", clientId.ToString()}
            };
            var hubConnection = new HubConnection("http://localhost:8080/", queryStringData);
            var proxy = hubConnection.CreateHubProxy("IpcHub");

            

            proxy.On("ShutDown", () => {
                Console.WriteLine("Shutdown order received");
                try {
                    hubConnection.Stop();
                } finally {
                    Environment.Exit(0);
                }
            });
            proxy.On("GetName", () => {
                proxy.Invoke("ReturnName", clientId, _name);
            });
            hubConnection.Start().Wait();
            Console.WriteLine("Connected to server");

            while (true) {
                Thread.Sleep(1000);
            }
        }

        private static void RunServer() {
            _name = "Server";
            using (WebApp.Start<Startup>("http://+:8080/")) {

                var hubConnection = new HubConnection("http://localhost:8080/");
                var proxy = hubConnection.CreateHubProxy("IpcHub");
                proxy.On("ShutDown", () => Console.WriteLine("Shutdown order received"));
                hubConnection.Start().Wait();

                var clients = new List<Process>();
                for (var i = 0; i < 4; i++) {
                    var startInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().ManifestModule.Name) {Arguments = i.ToString()};
                    var p = new Process {StartInfo = startInfo};
                    Console.WriteLine("Starting client " + i);
                    p.Start();
                    clients.Add(p);
                }

                Thread.Sleep(5000);
                for (var i = 0; i < 4; i++) {
                    Console.WriteLine("Getting Name from client " + i);
                    Console.WriteLine(proxy.Invoke<string>("GetName", i).Result);
                }
                Console.WriteLine("Press enter to shutdown...");
                Console.ReadLine();
                proxy.Invoke("ShutDown");
                while (clients.Any(p => !p.HasExited)) {
                    Thread.Sleep(100);
                }
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }
    }

    public class Startup {
        public void Configuration(IAppBuilder app) {
            app.MapSignalR();
        }
    }

    public class IpcHub : Hub {
        private static readonly ConcurrentDictionary<int, string> ConnectionMap = new ConcurrentDictionary<int, string>();
        private static bool _shutdownOrderReceived;

        public override Task OnConnected() {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server connected");
                _shutdownOrderReceived = false;
                return base.OnConnected();
            }
            var clientId = Convert.ToInt32(Context.QueryString["id"]);
            Console.WriteLine("Client {0} connected", clientId);
            ConnectionMap[clientId] = Context.ConnectionId;
            return base.OnConnected();
        }
        public override Task OnDisconnected(bool stopCalled) {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server disconnected");
                _shutdownOrderReceived = true;
                return base.OnDisconnected(stopCalled);
            }
            var clientId = Convert.ToInt32(Context.QueryString["id"]);
            Console.WriteLine("Client {0} disconnected", clientId);
            string dontCare;
            ConnectionMap.TryRemove(clientId, out dontCare);
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected() {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server reconnected");
                _shutdownOrderReceived = false;
                return base.OnReconnected();
            }
            var clientId = Convert.ToInt32(Context.QueryString["id"]);
            Console.WriteLine("Client {0} reconnected", clientId);
            ConnectionMap[clientId] = Context.ConnectionId;
            return base.OnReconnected();
        }

        public void ShutDown() {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server sends shutdown order");
                Clients.All.Shutdown();
            } else {
                Console.WriteLine("Received bad command from client {0} : ShutDown", Context.QueryString["id"]);
            }
        }

        private static readonly ConcurrentDictionary<int, string> ClientNames = new ConcurrentDictionary<int, string>(); 
        public string GetName(int clientId) {
            if (Context.QueryString["id"] == null) {
                if (ConnectionMap.ContainsKey(clientId)) {
                    Clients.Client(ConnectionMap[clientId]).GetName();
                    var ret = Clients.Client(ConnectionMap[clientId]).GetName();
                    return GetResult(clientId, ClientNames);
                }
            }
            return null;
        }

        public void ReturnName(int clientId, string name) {
            if (clientId == Convert.ToInt32(Context.QueryString["id"])) {
                ClientNames[clientId] = name;
            } else {
                Console.WriteLine("Bad return: qs client id={0}, parameter id={1}", Context.QueryString["id"],clientId);
            }
        }



        private static T GetResult<T, TKey>(TKey key, IDictionary<TKey, T> dataStore) {
            var i = 0;
            while (!dataStore.ContainsKey(key) && !_shutdownOrderReceived && i < 500) {
                Thread.Sleep(10);
                i++;
            }
            if (dataStore.ContainsKey(key)) {
                var ret = dataStore[key];
                dataStore.Remove(key);
                return ret;
            }
            return default(T);
        }
    }

}
