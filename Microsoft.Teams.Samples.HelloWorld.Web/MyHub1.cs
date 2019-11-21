using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.Teams.Samples.HelloWorld.Web
{
    public class ChatHub : Hub
    {
        public void Send(string name, string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.All.broadcastMessage(name, message);
        }
    }

    public class Broadcaster
    { 
        private readonly static Lazy<Broadcaster> _instance =
        new Lazy<Broadcaster>(() => new Broadcaster(GlobalHost.ConnectionManager.GetHubContext<ChatHub>().Clients));

        private static object mutex = new object();

        private Broadcaster(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            // Remainder of constructor ...
        }

        private IHubConnectionContext<dynamic> Clients
        {
            get;
            set;
        }

        private void BroadcastUpdate()
        {
            Clients.All.update();
        }

        public static Broadcaster Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        public static void Broadcast()
        {
            lock (mutex)
            {
                Instance.BroadcastUpdate();
            }
        }
    }
}