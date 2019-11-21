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
using System.Text.RegularExpressions;
using System.IO;
using System.Web;

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
        public string tenantId = "dunno";
        public string messageId = "dunno";
        public string channelId = "dunno";
        public string teamId = "dunno";
        public Activity BotFirstMessage = null;

        public static string Encode(string tenantId, string teamId, string channelId, string msgId)
            => $"{tenantId}__{teamId}__{channelId}__{msgId}".Replace(':', '_'); // avoid asp.net bad chars -- see https://www.hanselman.com/blog/ExperimentsInWackinessAllowingPercentsAnglebracketsAndOtherNaughtyThingsInTheASPNETIISRequestURL.aspx

        //public string Key => Encode(teamId, channelId, messageId);
    }

    public class QandAModelWrapper
    {
        public QandAModel model;
        public bool useRSC = true;
        public bool showLogin = true;
    }

    public class HomeController : Controller
    {
        [Route("")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("subscription")]
        [HttpPost]
        public ActionResult Subscription()
        {
            var encodedString = this.Request.QueryString["validationToken"];
            if (encodedString != null)
            {
                // Ack the webhook subscription
                var decodedString = HttpUtility.UrlDecode(encodedString);
                var res = new ContentResult() { Content = decodedString, ContentType = "text/plain", ContentEncoding = System.Text.Encoding.UTF8 };
                return res;
            } else
            {
                // signal clients
                Broadcaster.Broadcast();
                return new ContentResult() { Content = "", ContentType = "text/plain", ContentEncoding = System.Text.Encoding.UTF8 };
            }
        }

        [Route("Auth")]
        public ActionResult Auth()
        {
            return View("Auth");
        }

        [Route("authdone")]
        [HttpPost]
        public async Task<ActionResult> AuthDone()
        {
            string req_txt;
            using (StreamReader reader = new StreamReader(HttpContext.Request.InputStream))
            {
                req_txt = reader.ReadToEnd();
            }
            string token = ParseOauthResponse(req_txt);
            GraphServiceClient graph = GetAuthenticatedClient(token);
            var u = await (graph.Me.Request().GetAsync());

            return View("AuthDone");
        }

        // Also store the token in a cookie so the client can pass it back to us later
        public string ParseOauthResponse(string oathResponse)
        {
            //access_token =...
            //& token_type = Bearer
            //& expires_in = 3599
            //& id_token =...
            //& state = 75
            //& session_state = 430b10b4 - 262d - 49fe - af9d - e1fae258587b

            // Because of the way we have setup the url, idtoken comes in the body in xxx-form format.
            string access_token = oathResponse.Split('&')[0].Split('=')[1];
            string state = oathResponse.Split('&')[1].Split('=')[1];
            //string[] stateParts = Uri.UnescapeDataString(state).Split(new string[] { "__" }, StringSplitOptions.None);
            //string teamId = stateParts[0];
            //string channelId = stateParts[1];
            //string messageId = stateParts[2];

            //var resp =
            //await HttpHelpers.POST("https://login.microsoftonline.com/common/oauth2/v2.0/token",
            //    $"client_id={appId}" +
            //            $"&scope={Uri.EscapeDataString(graphScopes)}" +
            //            $"&code={authn_token}" +
            //            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            //            $"&grant_type=authorization_code" +
            //            $"&client_secret={Uri.EscapeDataString(appSecret)}"
            //            );

            //var bearer = JsonConvert.DeserializeObject<BearerResponse>(resp);
            //string token = bearer.access_token;

            Response.Cookies.Add(new System.Web.HttpCookie("GraphToken", access_token));
            return access_token;

            //Response.Cookies.Append("GraphToken", token);
            //string url = $"~/Home/QandA?teamId={teamId}&channelId={channelId}&messageId={messageId}";
            //return Redirect(url);
        }

        [Route("hello")]
        public ActionResult Hello()
        {
            return View("Index");
        }

        private static Dictionary<string, string> channelToSubscription
            = new Dictionary<string, string>();

        [Route("first")]
        public async Task<ActionResult> First(
            [FromUri(Name = "tenantId")] string tenantId,
            [FromUri(Name = "teamId")] string teamId,
            [FromUri(Name = "channelId")] string channelId,
            [FromUri(Name = "skipRefresh")] Nullable<bool> skipRefresh,
            [FromUri(Name = "useRSC")] Nullable<bool> useRSC
            )
        {
            bool usingRSC = (useRSC != false);

            string token;
            if (usingRSC)
            {
                token = await GetToken(tenantId);
            }
            else
            {
                var cookie = Request.Cookies["GraphToken"];
                token = cookie == null ? null : cookie.Value;
                //token = Request.Cookies["GraphToken"].Value;
            }
            //token = null;

            QandAModel model = GetModel(tenantId, teamId, channelId, "");
            QandAModelWrapper wrapper = new QandAModelWrapper() { model = model, useRSC = usingRSC, showLogin = (token == null) };

            try
            {
                GraphServiceClient graph = GetAuthenticatedClient(token);

                if (skipRefresh != true && !wrapper.showLogin)
                {
                    await RefreshQandA(model, graph);
                    if (usingRSC)
                    {
                        await CreateSubscription(channelId, model, graph);
                    }
                }
                ViewBag.MyModel = model;
                return View("First", wrapper);
                //return View("First", model);
            } catch (Exception e) when (e.Message.Contains("Unauthorized") || e.Message.Contains("Access token has expired."))
            {
                // token expired
                Response.Cookies.Remove("GraphToken");
                wrapper.showLogin = true;
                return View("First", wrapper);
            }
        }

        private static async Task CreateSubscription(string channelId, QandAModel model, GraphServiceClient graph)
        {
            var subscription = new Subscription
            {
                Resource = $"teams/{model.teamId}/channels/{model.channelId}/messages",
                ChangeType = "created,updated,deleted",
                NotificationUrl = ConfigurationManager.AppSettings["NotificationUrl"],
                ClientState = Guid.NewGuid().ToString(),
                ExpirationDateTime = DateTime.UtcNow + new TimeSpan(days: 0, hours: 0, minutes: 10, seconds: 0),
                IncludeProperties = true
            };

            try
            {
                if (channelToSubscription.ContainsKey(channelId))
                {
                    // refresh subscription
                    var subId = channelToSubscription[channelId];
                    var newSubscription = await graph.Subscriptions[subId].Request().UpdateAsync(subscription);
                }
                else
                {
                    try
                    {
                        var newSubscription = await graph.Subscriptions.Request().AddAsync(subscription);
                        channelToSubscription[channelId] = newSubscription.Id;
                    }
                    catch (Exception e) when (e.Message.Contains("has reached its limit of 1 TEAMS"))
                    {
                        // ignore, we're still being notified
                    }
                }
            }
            catch (Exception)
            {
                // Bail on subscriptions without killing the whole demo
            }
        }

        [Route("Home/MarkAsAnswered")]
        public ActionResult MarkAsAnswered(
            [FromUri(Name = "tenantId")] string tenantId,
            [FromUri(Name = "teamId")] string teamId,
            [FromUri(Name = "channelId")] string channelId,
            [FromUri(Name = "messageId")] string messageId)
            //,
            //[FromQuery(Name = "replyId")] string replyId)
        {
            QandAModel model = GetModel(tenantId, teamId, channelId, ""); //messageId);
            //model.IsQuestionAnswered[replyId] = true;
            model.IsQuestionAnswered[messageId] = true;
            string url = $"~/First?tenantId={tenantId}&teamId={teamId}&channelId={channelId}&messageId={messageId}&skipRefresh=true&useRSC=false";
            return Redirect(url);
        }

        [Route("Home/MarkAsUnanswered")]
        public ActionResult MarkAsUnanswered(
            [FromUri(Name = "tenantId")] string tenantId,
            [FromUri(Name = "teamId")] string teamId,
            [FromUri(Name = "channelId")] string channelId,
            [FromUri(Name = "messageId")] string messageId)
        //,
        //[FromQuery(Name = "replyId")] string replyId)
        {
            QandAModel model = GetModel(tenantId, teamId, channelId, ""); //messageId);
            //model.IsQuestionAnswered[replyId] = true;
            model.IsQuestionAnswered[messageId] = false;
            string url = $"~/First?tenantId={tenantId}&teamId={teamId}&channelId={channelId}&messageId={messageId}&skipRefresh=true&useRSC=false";          
            return Redirect(url);
        }


        public async Task RefreshQandA(QandAModel qAndA, GraphServiceClient graph)
        {
            var handle = graph.Teams[qAndA.teamId].Channels[qAndA.channelId]
                .Messages.Request().Top(30);
            try
            {
                var msgs = await handle.GetAsync();
                //var msgs = await graph.Teams[qAndA.teamId].Channels[qAndA.channelId]
                //    .Messages.Request().Top(30).GetAsync();
                ////var msgs = await graph.Teams[qAndA.teamId].Channels[qAndA.channelId]
                //    .Messages[qAndA.messageId].Replies.Request().Top(50).GetAsync();

                // merge w/ existing questions 
                var questions =
                    from m in msgs
                    where IsQuestion(m)
                    select new Question()
                    {
                        MessageId = m.Id,
                        Text = StripHTML(m.Body.Content),
                        Votes = m.Reactions.Count()
                    };
                qAndA.Questions = questions.OrderByDescending(m => m.Votes).ToList();

                foreach (var q in questions)
                {
                    if (!qAndA.IsQuestionAnswered.ContainsKey(q.MessageId))
                        qAndA.IsQuestionAnswered[q.MessageId] = false;
                }

                //await UpdateCard(qAndA);
            } catch (Exception e)
            {
                string m = String.Format("{0}\n {1}\n {2}\n {3}\n --- trace {4}", handle.GetHttpRequestMessage().GetRequestContext().ClientRequestId,
                    handle.GetHttpRequestMessage().Method,
                    handle.GetHttpRequestMessage().RequestUri,
                    handle.GetHttpRequestMessage().Content,
                    //handle.GetHttpRequestMessage().Headers.Authorization.Parameter,
                    e.StackTrace);
                throw new Exception(m);
            }
        }

        public static string StripHTML(string input)
        {
            return Regex.Replace(input, "<.*?>", String.Empty);
        }

        private bool IsQuestion(ChatMessage message)
        {
            if (!StripHTML(message.Body.Content).Contains("?")) // no ? in message
                return false;
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


        private QandAModel GetModel(string tenantId, string teamId, string channelId, string messageId)
        {
            string key = QandAModel.Encode(tenantId, teamId, channelId, messageId);
            QandAModel model;
            if (QandAModel.qAndALookup.ContainsKey(key))
            {
                model = QandAModel.qAndALookup[key];
            }
            else
            {
                model = new QandAModel() { tenantId = tenantId, teamId = teamId, channelId = channelId, messageId = messageId };
                QandAModel.qAndALookup[key] = model;
            }
            return model;
        }

        private static async Task<string> GetToken(string tenant)
        {
            string appId = "cb38cf54-ac89-4a7a-9ea3-095d3d080037";// ConfigurationManager.AppSettings["ida:GraphAppId"];
            string redirectUri = ConfigurationManager.AppSettings["ida:RedirectUri"];
            string appSecret = Uri.EscapeDataString("oj/2aJkt391=rZEpIzfxIkvTKbjIKV][");
            //ConfigurationManager.AppSettings["ida:GraphAppPassword"]);
            //string tenant = "139d16b4-7223-43ad-b9a8-674ba63c7924";

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
