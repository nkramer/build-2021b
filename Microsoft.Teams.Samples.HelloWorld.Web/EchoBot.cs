using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams;
using System.Configuration;
using System;

namespace Microsoft.Teams.Samples.HelloWorld.Web
{
    public class EchoBot
    {
        public static async Task EchoMessage(ConnectorClient connector, Activity activity)
        {
            string token = await GetToken();
            var reply = activity.CreateReply("You said!: " + activity.GetTextWithoutMentions());
            await connector.Conversations.ReplyToActivityWithRetriesAsync(reply);
        }


        private  static async Task<string> GetToken()
        {
            string appId = "cb38cf54-ac89-4a7a-9ea3-095d3d080037";// ConfigurationManager.AppSettings["ida:GraphAppId"];
            string redirectUri = ConfigurationManager.AppSettings["ida:RedirectUri"];
            string appSecret = Uri.EscapeDataString("oj/2aJkt391=rZEpIzfxIkvTKbjIKV][");
            //ConfigurationManager.AppSettings["ida:GraphAppPassword"]);
            string tenant = "139d16b4-7223-43ad-b9a8-674ba63c7924";

            string response = await HttpHelpers.POST($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
                    $"grant_type=client_credentials&client_id={appId}&client_secret={appSecret}"
                    + "&scope=https%3A%2F%2Fgraph.microsoft.com%2F.default");
            string token = response.Deserialize<TokenResponse>().access_token;
            return token;
        }
    }

    // Used only for de-serializing JSON
    public class TokenResponse
    {
        public string access_token { get; set; }
    }

}
