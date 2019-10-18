using Microsoft.Bot.Connector;
using Microsoft.Graph;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Mvc;
using FromUriAttribute = System.Web.Http.FromUriAttribute;

namespace Microsoft.Teams.Samples.HelloWorld.Web.Controllers
{
    public class TeamAndChannel
    {
        public string teamId;
        public string channelId;
        public string message;
    }

    public class Question
    {
        public string MessageId;
        public int Votes;
        public string Text;
        //        public bool IsAnswered;
    }

    public class QandAModel
    {
        public static readonly Dictionary<string, QandAModel> qAndALookup = new Dictionary<string, QandAModel>();

        public List<Question> Questions = new List<Question>();
        public Dictionary<string, bool> IsQuestionAnswered = new Dictionary<string, bool>(); // maps message id -> isAnswered
        public string messageId = "dunno";
        public string channelId = "dunno";
        public string teamId = "dunno";
        public Activity BotFirstMessage = null;

        public static string Encode(string teamId, string channelId, string msgId)
            => $"{teamId}__{channelId}__{msgId}".Replace(':', '_'); // avoid asp.net bad chars -- see https://www.hanselman.com/blog/ExperimentsInWackinessAllowingPercentsAnglebracketsAndOtherNaughtyThingsInTheASPNETIISRequestURL.aspx

        public string Key => Encode(teamId, channelId, messageId);
    }

    public class HomeController : Controller
    {
        [Route("")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("hello")]
        public ActionResult Hello()
        {
            return View("Index");
        }

        [Route("first")]
        public async Task<ActionResult> First(
            [FromUri(Name = "teamId")] string teamId,
            [FromUri(Name = "channelId")] string channelId)
        {
            string token = await GetToken();
            GraphServiceClient graph = GetAuthenticatedClient(token);

            QandAModel model = GetModel(teamId, channelId,  "");
            await RefreshQandA(model, graph);
            ViewBag.MyModel = model;
            //return View("QandAView", model);
            return View("First", model);

            //string messages = await new HttpHelpers(token).HttpGetJson($"/teams/{teamId}/channels/{channelId}/messages");


            //return View(new TeamAndChannel() { teamId = teamId, channelId = channelId,
            //message=messages});
        }

        public async Task RefreshQandA(QandAModel qAndA, GraphServiceClient graph)
        {
            var msgs = await graph.Teams[qAndA.teamId].Channels[qAndA.channelId]
                .Messages.Request().Top(20).GetAsync();
            //var msgs = await graph.Teams[qAndA.teamId].Channels[qAndA.channelId]
            //    .Messages[qAndA.messageId].Replies.Request().Top(50).GetAsync();

            // merge w/ existing questions 
            var questions =
                from m in msgs
                where IsQuestion(m)
                select new Question()
                {
                    MessageId = m.Id,
                    Text = m.Body.Content,
                    Votes = m.Reactions.Count()
                };
            qAndA.Questions = questions.OrderByDescending(m => m.Votes).ToList();

            foreach (var q in questions)
            {
                if (!qAndA.IsQuestionAnswered.ContainsKey(q.MessageId))
                    qAndA.IsQuestionAnswered[q.MessageId] = false;
            }

            //await UpdateCard(qAndA);
        }

        private bool IsQuestion(ChatMessage message)
        {
            if (message.From.User == null)  // sender is bot
                return false;
            if (message.Mentions == null) // no @mention
                return true;
            foreach (var men in message.Mentions)
            {
                var app = men.Mentioned.Application;
                if (app != null
                    && (app.AdditionalData["applicationIdentityType"] as string) == "bot")
                    return false;
            }
            return true;
        }


        private QandAModel GetModel(string teamId, string channelId, string messageId)
        {
            string key = QandAModel.Encode(teamId, channelId, messageId);
            QandAModel model;
            if (QandAModel.qAndALookup.ContainsKey(key))
            {
                model = QandAModel.qAndALookup[key];
            }
            else
            {
                model = new QandAModel() { teamId = teamId, channelId = channelId, messageId = messageId };
                QandAModel.qAndALookup[key] = model;
            }
            return model;
        }

        private static async Task<string> GetToken()
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

        [Route("second")]
        public ActionResult Second()
        {
            return View();
        }

        [Route("configure")]
        public ActionResult Configure()
        {
            return View();
        }
    }
}
