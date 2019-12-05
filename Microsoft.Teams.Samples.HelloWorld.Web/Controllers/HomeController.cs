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
using System.Net;
using System.Diagnostics;

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

    internal static class Authorization
    {
        // As part of creating the Graph Client, this method acquires all the necessary 
        // tokens, and checks that the user has access to the team.
        public static async Task<GraphServiceClient> GetGraphClient(string teamId, HttpCookieCollection requestCookies, HttpCookieCollection responseCookies, Nullable<bool> useRSC)
        {
            // There's potentially two tokens – the user delegated token, which provide the user's identity, 
            // and the main token which allows the app to make useful API calls. When not using RSC, it's 
            // all the same token. But when using RSC, the second token is an RSC/application permissions token.
            // I recommend using the same appid for both tokens, but you don't have to.
            bool usingRSC = (useRSC != false);

            string userToken = GetTokenFromCookie(requestCookies);

            if (userToken == null)
                FailAuth(requestCookies, responseCookies);

            // For debugging sanity, make sure the cookie is from the app ID you are actually 
            // expecting, and not a leftover of a previous version of your app.
            if (GetTokenClaim(userToken, "appid") != GetGraphAppId(useRSC))
                FailAuth(requestCookies, responseCookies);

            // Figure out the user and tenant from the userToken. The information is all in the token, 
            // but the easiest way to verify the token is properly signed is to use it to make a Graph call.
            GraphServiceClient userGraph = GetGraphClientUnsafe(userToken);
            User me = null;
            try
            {
                me = await userGraph.Me.Request().GetAsync();
            }
            catch
            {
                // eg InvalidAuthenticationToken
                FailAuth(requestCookies, responseCookies);
            }
            string tenantId = GetTenant(userToken);

            string messagingToken =
                usingRSC
                ? await GetAppPermissionToken(tenantId, useRSC)
                : userToken;

            GraphServiceClient messagingGraph = GetGraphClientUnsafe(messagingToken);

            var members = await messagingGraph.Groups[teamId].Members.Request().GetAsync();
            if (!members.Any(member => member.Id == me.Id))
                FailAuth(requestCookies, responseCookies);
            // to do - figure out if this handles paging, for the case where there's more than 500 users in a team

            // Alternate approach:
            //bool userIsMember = false;
            //var checks = graph.Groups[teamId].CheckMemberObjects(new string[] { UserFromToken() }).Request().PostAsync();
            //foreach (var c in await checks)
            //{
            //    if (c == UserFromToken())
            //        userIsMember = true;
            //}

            return GetGraphClientUnsafe(messagingToken);
        }

        private static string GetTokenFromCookie(HttpCookieCollection cookies)
        {
            var cookie = cookies["GraphToken"];

            if (cookie == null)
                return null;
            else
                return cookie.Value;
        }

        private static void FailAuth(HttpCookieCollection requestCookies, HttpCookieCollection responseCookies)
        {
            if (responseCookies["GraphToken"] != null)
            {
                // Remove invalid cookie by expiring it
                responseCookies["GraphToken"].Expires = DateTime.Now.AddDays(-1);
                responseCookies["GraphToken"].Value = "invalid";
            }

            throw new Exception("Unauthorized user!");
        }

        private static string GetTokenClaim(string token, string claimType)
        {
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            foreach (var claim in jwt.Claims)
            {
                if (claim.Type == claimType)
                    return claim.Value;
            }

            return null;
        }

        private static string GetTenant(string token)
        {
            return GetTokenClaim(token, "tid");
        }

        private static async Task<string> GetAppPermissionToken(string tenant, Nullable<bool> useRSC)
        {
            string appId = GetGraphAppId(useRSC);
            string appSecret = Uri.EscapeDataString(GetGraphAppPassword(useRSC));

            string response = await HttpHelpers.POST($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
                    $"grant_type=client_credentials&client_id={appId}&client_secret={appSecret}"
                    + "&scope=https%3A%2F%2Fgraph.microsoft.com%2F.default");
            string token = response.Deserialize<TokenResponse>().access_token;
            return token;
        }

        private static GraphServiceClient GetGraphClientUnsafe(string token)
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

        public static void ProcessAadCallbackAndStoreUserToken(HttpContextBase httpContext, HttpCookieCollection cookies)
        {
            string req_txt;
            using (StreamReader reader = new StreamReader(httpContext.Request.InputStream))
            {
                req_txt = reader.ReadToEnd();
            }
            string token = Authorization.ParseOauthResponse(req_txt);
            cookies.Add(new System.Web.HttpCookie("GraphToken", token));

            // Ideally we would store the token on the server and never send it to the 
            // client, since in this app the client doesn't make Graph calls directly. 
            // But it's not a big deal to send the user delegated token down, 
            // and if the client app actually used it we would do so without reservation.
            // Never put an application permissions token in a cookie, though.
        }

        // Also store the token in a cookie so the client can pass it back to us later
        private static string ParseOauthResponse(string oathResponse)
        {
            //access_token =...
            //& token_type = Bearer
            //& expires_in = 3599
            //& id_token =...
            //& state = 75
            //& session_state = 430b10b4 - 262d - 49fe - af9d - e1fae258587b

            // Because of the way we have setup the url, idtoken comes in the body in xxx-form format.
            Debug.Assert(oathResponse.Split('&')[0].Split('=')[0] == "access_token");
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

            return access_token;

            //Response.Cookies.Append("GraphToken", token);
            //string url = $"~/Home/QandA?teamId={teamId}&channelId={channelId}&messageId={messageId}";
            //return Redirect(url);
        }

        // Returns the appid for user delegated use
        public static string GetGraphAppId(Nullable<bool> useRSC)
        {
            bool usingRSC = (useRSC != false);
            if (usingRSC)
                return ConfigurationManager.AppSettings["GraphRSCAppId"];
            else
                return ConfigurationManager.AppSettings["GraphNoRSCAppId"];
        }

        // Returns the appid for user delegated use
        private static string GetGraphAppPassword(Nullable<bool> useRSC)
        {
            bool usingRSC = (useRSC != false);
            if (usingRSC)
                return ConfigurationManager.AppSettings["GraphRSCAppPassword"];
            else
                return ConfigurationManager.AppSettings["GraphNoRSCAppPassword"];
        }
    }

    public class AuthModel
    {
        public string GraphAppId;
    }

    public class HomeController : Controller
    {
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
            try
            {
                // Do our auth check first
                GraphServiceClient graph = await Authorization.GetGraphClient(teamId, Request.Cookies, Response.Cookies, useRSC);

                QandAModel model = GetModel(tenantId, teamId, channelId, "");
                QandAModelWrapper wrapper = new QandAModelWrapper() {
                    useRSC = usingRSC,
                    showLogin = false,
                    model = model
                };

                if (skipRefresh != true)
                {
                    await RefreshQandA(model, graph);
                    if (usingRSC) // not available w/ user delegated permissions
                    {
                        await CreateSubscription(channelId, model, graph);
                    }
                }
                ViewBag.MyModel = model;
                return View("First", wrapper);
            }
            catch (Exception e) when (e.Message.Contains("Unauthorized") || e.Message.Contains("Access token has expired."))
            {
                // missing, bad, or expired token
                QandAModelWrapper wrapper = new QandAModelWrapper()
                {
                    useRSC = usingRSC,
                    showLogin = true,
                    model = null
                };
                return View("First", wrapper);
            }
        }

        // Start user auth flow -- get name & consent
        [Route("AuthRSC")]
        public ActionResult AuthRSC()
        {
            bool useRSC = true;
            var model = new AuthModel() { GraphAppId = Authorization.GetGraphAppId(useRSC) };
            return View("Auth", model);
        }

        // Start user auth flow -- get name & consent
        [Route("AuthNoRSC")]
        public ActionResult AuthNoRSC()
        {
            bool useRSC = false;
            var model = new AuthModel() { GraphAppId = Authorization.GetGraphAppId(useRSC) };
            return View("Auth", model);
        }

        // AAD callback
        [Route("authdone")]
        [HttpPost]
        public async Task<ActionResult> AuthDone()
        {
            Authorization.ProcessAadCallbackAndStoreUserToken(this.HttpContext, this.Response.Cookies);
            return View("AuthDone");
        }

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

        private static Dictionary<string, string> channelToSubscription
            = new Dictionary<string, string>();

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

        // Callback
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
            }
            else
            {
                // signal clients
                Broadcaster.Broadcast();
                return new ContentResult() { Content = "", ContentType = "text/plain", ContentEncoding = System.Text.Encoding.UTF8 };
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
            
            // to-do -- consider escaping these parameters, even though they aren't trusted on the other end
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
                string m = String.Format("{0}\n {1}\n {2}\n {3}\n --- trace {4}", 
                    handle.GetHttpRequestMessage().GetRequestContext().ClientRequestId,
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
            // TODO: Validate the user has access to the team (or at least the tenant) before retrieving the model. 
            // It's not critical right now since we'll fail out when we make the inevitable Graph calls to
            // update the model, but it's a little fragile.

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
