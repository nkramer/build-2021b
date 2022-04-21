namespace Microsoft.Teams.Samples.Build22.Controllers
{
    using Microsoft.Graph;
    using Microsoft.Teams.Samples.HelloWorld.Web.Controllers;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.Mvc;

    [Route("closeChats")]
    public class CloseChatController : Controller
    {
        // GET: CloseChat
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task CloseChat(string chatId)
        {
            GraphServiceClient userContextClient = await Authorization.GetGraphClientInUserContext().ConfigureAwait(false);

            // Remove self from chat
            IChatMembersCollectionPage members = await userContextClient.Chats[chatId].Members.Request().GetAsync().ConfigureAwait(false);
            ConversationMember memberToRemove = members.CurrentPage.First(member => (member as AadUserConversationMember).UserId == "82fe7758-5bb3-4f0d-a43f-e555fd399c6f");

            await userContextClient.Chats[chatId].Members[memberToRemove.Id].Request().DeleteAsync().ConfigureAwait(false);

            var user = new TeamworkUserIdentity
            {
                Id = "82fe7758-5bb3-4f0d-a43f-e555fd399c6f"
            };

            var tenantId = "2432b57b-0abd-43db-aa7b-16eadd115d34";

            // Hide chat from viewpoint
            await userContextClient.Chats[chatId].HideForUser(user, tenantId).Request().PostAsync().ConfigureAwait(false);
        }
    }
}