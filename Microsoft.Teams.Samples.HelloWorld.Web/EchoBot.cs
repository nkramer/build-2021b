using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams;
using System.Configuration;
using System;
using System.Text.RegularExpressions;
using Microsoft.Graph;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Bot.Connector.Teams.Models;

namespace Microsoft.Teams.Samples.HelloWorld.Web
{
    public class EchoBot
    {
        public static async Task EchoMessage(ConnectorClient connector, Activity activity)
        {
            if (activity is IMessageActivity)
            {
                if (activity.GetTextWithoutMentions() != "")
                {
                    //string token = await GetToken();
                    //GraphServiceClient graph = GetAuthenticatedClient(token);
                    //TeamsConnectorClient teamsConnector = connector.GetTeamsConnectorClient();
                    //string fakeTeamId = activity.GetChannelData<TeamsChannelData>().Team.Id;
                    //string teamId = (await teamsConnector.Teams.FetchTeamDetailsAsync(fakeTeamId)).AadGroupId;
                    //string channelId = activity.GetChannelData<TeamsChannelData>().Channel.Id;

                    //string messages = await new HttpHelpers(token).HttpGetJson($"/teams/{teamId}/channels/{channelId}/messages");
                    //string msgs = await GetAllMessages(graph, teamId, channelId);
                    //var response = activity.CreateReply("Here you go:\n" + messages);
                    string input = activity.Text;
                    input = input.Replace("<at>cTrade</at> ", "");
                    string[] words = input.Split(' ');
                    words[0] = words[0].ToLower();
                    string reply =
                        (words[0].StartsWith("meet")) ? "Meeting scheduled for 2pm."
                        : (words[0].StartsWith("schedule")) ? "Meeting scheduled for 2pm."
                        : (words[0].StartsWith("buy")) ? $"Bought {words[1]} shares."
                        : "Huh?";

                    var response = activity.CreateReply(reply);
                    //var response = activity.CreateReply("Bob Smith said:\n" + "See you in Dallas!");
                    await connector.Conversations.ReplyToActivityWithRetriesAsync(response);
                }

                //var reply = activity.CreateReply("You said!: " + activity.GetTextWithoutMentions());
                //await connector.Conversations.ReplyToActivityWithRetriesAsync(reply);
            }
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

        public static GraphServiceClient GetAuthenticatedClient(string token)
        {
            var graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    requestMessage =>
                    {
                        // Append the access token to the request.
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);

                        // Get event times in the current time zone.
                        requestMessage.Headers.Add("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\"");

                        return Task.CompletedTask;
                    }));
            return graphClient;
        }



        private static string MsgToString(ChatMessage msg)
        {
            string from = "???";
            if (msg.From.User != null)
                from = msg.From.User.DisplayName;
            else if (msg.From.Application != null)
                from = msg.From.Application.DisplayName;
            string body = StripHTML(msg.Body.Content).Replace("&nbsp;", " ");
            return $"[{from} at {msg.CreatedDateTime.Value.Date.ToShortTimeString()} "
                + $"{msg.CreatedDateTime.Value.Date.ToShortDateString()}]\n"
                + body + "\n\n";
        }

        // https://stackoverflow.com/questions/18153998/how-do-i-remove-all-html-tags-from-a-string-without-knowing-which-tags-are-in-it
        public static string StripHTML(string input)
        {
            return Regex.Replace(input, "<.*?>", String.Empty);
        }

        public async static Task<string> GetAllMessages(GraphServiceClient graph,
            string teamId, string channelId)
        {
            var messages = await graph.Teams[teamId].Channels[channelId]
                .Messages.Request().Top(10).GetAsync();
            var builder = new StringBuilder();
            foreach (var message in messages)
            {
                builder.Append(MsgToString(message));
                //var replies = await graph.Teams[teamId].Channels[channelId]
                //    .Messages[message.Id].Replies.Request().Top(50).GetAsync();
                //builder.Append(String.Join("", replies.Select(r => MsgToString(r)).ToArray()));
            }
            return builder.ToString();
        }

        public static async Task ArchiveChannel(GraphServiceClient graph,
            string teamId, string channelId)
        {
            //Activity reply = turnContext.Activity.CreateReply("Archiving!");
            //await turnContext.SendActivityAsync(reply);

            string all = await GetAllMessages(graph, teamId, channelId);

            // save to database of record
            Debug.WriteLine(all);

            //// Rename the channel before we delete it so it doesn't interfere 
            //// with future channels we might want to create
            //var archivedChannel = await graph.Teams[teamId].Channels[channelId].Request().GetAsync();
            //string archivedChannelName = archivedChannel.DisplayName;
            //archivedChannel.DisplayName = archivedChannel.DisplayName + " - " + DateTime.Now.ToString();
            //await graph.Teams[teamId].Channels[channelId].Request().UpdateAsync(archivedChannel);
            //await graph.Teams[teamId].Channels[channelId].Request().DeleteAsync();

            //// tell user channel is archived
            //var channels = await graph.Teams[teamId].Channels.Request().GetAsync();
            //var generalChannel = channels.Where(c => c.DisplayName == "General").First();

            //var message = Activity.CreateMessageActivity();
            //message.Text = $"{archivedChannelName} channel has been archived.";

            //await SendNewMessage(turnContext, graph, generalChannel, (Activity)message);
        }

    }

    // Used only for de-serializing JSON
    public class TokenResponse
    {
        public string access_token { get; set; }
    }

}
