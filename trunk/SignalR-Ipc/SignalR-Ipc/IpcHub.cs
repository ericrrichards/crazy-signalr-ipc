using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNet.SignalR;

namespace SignalR_Ipc {
    public class IpcHub : Hub {
        // A map between the child clients and their SignalR connection Ids
        private static readonly ConcurrentDictionary<int, string> ConnectionMap = new ConcurrentDictionary<int, string>();
        private static bool _shutdownOrderReceived;

        // Invoked when a client connects to the hub
        public override Task OnConnected() {
            if (Context.QueryString["id"] == null) {
                // The server doesn't have an id in this case
                // in real usage you may want to authenticate the server in some way
                Console.WriteLine("Server connected");
                _shutdownOrderReceived = false;
                return base.OnConnected();
            }
            // When a client connects, we cache the SignalR connection ID in a dictionary that maps the client ID to the connection ID
            var clientId = Convert.ToInt32(Context.QueryString["id"]);
            Console.WriteLine("Client {0} connected", clientId);
            ConnectionMap[clientId] = Context.ConnectionId;
            return base.OnConnected();
        }
        // Invoked when a client disconnects from the hub
        public override Task OnDisconnected(bool stopCalled) {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server disconnected");
                _shutdownOrderReceived = true;
                Clients.All.Shutdown();
                return base.OnDisconnected(stopCalled);
            }
            var clientId = Convert.ToInt32(Context.QueryString["id"]);
            Console.WriteLine("Client {0} disconnected", clientId);
            string dontCare;
            ConnectionMap.TryRemove(clientId, out dontCare);
            return base.OnDisconnected(stopCalled);
        }
        // Invoked when a temporary network connection error interrupts a client's connection to the hub
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

        [UsedImplicitly]
        public void ShutDown() {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server sends shutdown order");
                Clients.All.Shutdown();
                _shutdownOrderReceived = true;
            } else {
                Console.WriteLine("Received bad command from client {0} : ShutDown", Context.QueryString["id"]);
            }
        }

        // A dictionary to temporarily store values posted by the clients before returning them to the server
        // the values are indexed by the client ids
        private static readonly ConcurrentDictionary<int, string> ClientNames = new ConcurrentDictionary<int, string>();
        [UsedImplicitly]
        public string GetName(int clientId) {
            if (Context.QueryString["id"] == null) {
                if (ConnectionMap.ContainsKey(clientId)) {
                    // order the client to return its name
                    Clients.Client(ConnectionMap[clientId]).GetName();
                    // block until the name is received from the client
                    return GetResult(clientId, ClientNames);
                }
            }
            return null;
        }

        [UsedImplicitly]
        // When the client pushes its data, store the value in the dictionary
        public void ReturnName(int clientId, string name) {
            if (clientId == Convert.ToInt32(Context.QueryString["id"])) {
                ClientNames[clientId] = name;
            } else {
                Console.WriteLine("Bad return: qs client id={0}, parameter id={1}", Context.QueryString["id"], clientId);
            }
        }

        private const int TimeOut = 500;
        // block until the requested key is populated in the dictionary
        // return a default value if something goes wrong and the client does not push the value within the timeout period
        // or if the server has ordered a shutdown before the value is returned
        private static T GetResult<T, TKey>(TKey key, ConcurrentDictionary<TKey, T> dataStore) {
            var i = 0;
            while (!dataStore.ContainsKey(key) && !_shutdownOrderReceived && i < TimeOut) {
                Thread.Sleep(10);
                i++;
            }
            if (dataStore.ContainsKey(key)) {
                T ret;
                dataStore.TryRemove(key, out ret);
                return ret;
            }
            return default(T);
        }
    }
}