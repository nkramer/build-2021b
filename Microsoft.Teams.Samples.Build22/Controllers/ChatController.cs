namespace Microsoft.Teams.Samples.HelloWorld.Web.Controllers
{
    using Microsoft.Graph;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using System.Web.Helpers;
    using System.Web.Mvc;

    [Route("chats")]
    public class ChatController : Controller
    {
        // GET: Chat
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> CreateChat()
        {
            GraphServiceClient appContextClient = await Authorization.GetGraphClientInAppContext().ConfigureAwait(false);
            GraphServiceClient userContextClient = await Authorization.GetGraphClientInUserContext().ConfigureAwait(false);

            Chat newChat = new Chat
            {
                ChatType = ChatType.Group,
                Topic = $"Help Chat #{new Random().Next(200)}",
                Members = new ChatMembersCollectionPage
                {
                    new AadUserConversationMember
                    {
                        Roles = new List<string> { "owner" },
                        AdditionalData = new Dictionary<string, object>()
                        {
                            {"user@odata.bind", "https://graph.microsoft.com/v1.0/users('c27c1b19-3904-4822-9813-4f6bdaab2eae')"}
                        }
                    },
                    new AadUserConversationMember
                    {
                        Roles = new List<string> { "owner" },
                        AdditionalData = new Dictionary<string, object>()
                        {
                            {"user@odata.bind", "https://graph.microsoft.com/v1.0/users('4b822dfc-2864-44e6-aa1e-7e0e8552168f')"}
                        }
                    },
                    new AadUserConversationMember
                    {
                        Roles = new List<string> { "owner" },
                        AdditionalData = new Dictionary<string, object>()
                        {
                            {"user@odata.bind", "https://graph.microsoft.com/v1.0/users('82fe7758-5bb3-4f0d-a43f-e555fd399c6f')"}
                        }
                    }
                },
            };

            Chat createdChat = await appContextClient.Chats.Request().AddAsync(newChat).ConfigureAwait(false);

            byte[] imageBytes = System.IO.File.ReadAllBytes(Server.MapPath("~/Content/") + "mockup.png");

            var chatMessage = new ChatMessage
            {
                Subject = null,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = "<attachment id=\"74d20c7f34aa4a7fb74e2b30004247c5\"></attachment>"
                },
                Attachments = new List<ChatMessageAttachment>()
                {
                    new ChatMessageAttachment
                    {
                        Id = "74d20c7f34aa4a7fb74e2b30004247c5",
                        ContentType = "application/vnd.microsoft.card.adaptive",
                        ContentUrl = null,
                        Content = "{\r\n  \"$schema\": \"http://adaptivecards.io/schemas/adaptive-card.json\",\r\n  \"type\": \"AdaptiveCard\",\r\n  \"version\": \"1.2\",\r\n  \"speak\": \"The Seattle Seahawks beat the Carolina Panthers 40-7\",\r\n  \"body\": [\r\n    {\r\n      \"type\": \"Container\",\r\n      \"items\": [\r\n        {\r\n           \"type\": \"Image\",\r\n           \"url\": \"../hostedContents/1/$value\",\r\n           \"size\": \"\"\r\n        },\r\n        {\r\n           \"type\": \"TextBlock\",\r\n           \"text\": \"Looking for advice on this stock. Please advise.\",\r\n \"wrap\": true\r\n        }\r\n      ]\r\n    }\r\n  ]\r\n}",
                        Name = null,
                        ThumbnailUrl = null
                    }
                },
                HostedContents = new ChatMessageHostedContentsCollectionPage()
                {
                    new ChatMessageHostedContent
                    {
                        ContentBytes = imageBytes,
                        ContentType = "image/png",
                        AdditionalData = new Dictionary<string, object>()
                        {
                            {"@microsoft.graph.temporaryId", "1"}
                        }
                    }
                }
            };

            Task createMessageTask = userContextClient.Chats[createdChat.Id].Messages.Request().AddAsync(chatMessage);

            // TODO: Install "this" app into the chat so that we can "communicate" with the bot.
            TeamsAppInstallation teamsAppInstallation = new TeamsAppInstallation
            {
                AdditionalData = new Dictionary<string, object>
                {
                    {"teamsApp@odata.bind", $"https://graph.microsoft.com/beta/appCatalogs/teamsApps/{ConfigurationManager.AppSettings["TeamsAppId"]}"}
                }
            };

            Task installAppToChatTask = userContextClient.Chats[createdChat.Id].InstalledApps.Request().AddAsync(teamsAppInstallation);

            await Task.WhenAll(createMessageTask, installAppToChatTask).ConfigureAwait(false);

            return Json(new { success = true, chatId = createdChat.Id });
        }
    }
}