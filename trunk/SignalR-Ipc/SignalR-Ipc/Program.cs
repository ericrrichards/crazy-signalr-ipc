using System;
using System.Linq;
using JetBrains.Annotations;
using Owin;

namespace SignalR_Ipc {
    static class Program {
        static void Main(string[] args) {

            if (args.Length == 0) {
                var server = new Server(4);
                server.Run();
            } else {
                var clientId = Convert.ToInt32(args.FirstOrDefault());
                var client = new Client(clientId);
                client.Run();
            }
        }
    }

    public class Startup {
        [UsedImplicitly]
        public void Configuration(IAppBuilder app) {
            app.MapSignalR();
        }
    }
}
