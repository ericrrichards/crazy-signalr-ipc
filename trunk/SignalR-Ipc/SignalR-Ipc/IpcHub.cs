using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNet.SignalR;

namespace SignalR_Ipc {
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

        [UsedImplicitly]
        public void ShutDown() {
            if (Context.QueryString["id"] == null) {
                Console.WriteLine("Server sends shutdown order");
                Clients.All.Shutdown();
            } else {
                Console.WriteLine("Received bad command from client {0} : ShutDown", Context.QueryString["id"]);
            }
        }

        private static readonly ConcurrentDictionary<int, string> ClientNames = new ConcurrentDictionary<int, string>();

        [UsedImplicitly]
        public string GetName(int clientId) {
            if (Context.QueryString["id"] == null) {
                if (ConnectionMap.ContainsKey(clientId)) {
                    Clients.Client(ConnectionMap[clientId]).GetName();
                    return GetResult(clientId, ClientNames);
                }
            }
            return null;
        }

        [UsedImplicitly]
        public void ReturnName(int clientId, string name) {
            if (clientId == Convert.ToInt32(Context.QueryString["id"])) {
                ClientNames[clientId] = name;
            } else {
                Console.WriteLine("Bad return: qs client id={0}, parameter id={1}", Context.QueryString["id"], clientId);
            }
        }

        private static T GetResult<T, TKey>(TKey key, ConcurrentDictionary<TKey, T> dataStore) {
            var i = 0;
            while (!dataStore.ContainsKey(key) && !_shutdownOrderReceived && i < 500) {
                Thread.Sleep(10);
                i++;
            }
            if (dataStore.ContainsKey(key)) {
                T ret;
                dataStore.TryRemove(key,out ret);
                return ret;
            }
            return default(T);
        }
    }
}