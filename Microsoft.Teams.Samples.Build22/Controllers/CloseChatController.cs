namespace Microsoft.Teams.Samples.Build22.Controllers
{
    using Microsoft.Graph;
    using Microsoft.Teams.Samples.HelloWorld.Web.Controllers;
    using System.Configuration;
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
            string userId = ConfigurationManager.AppSettings["GraphUserId"];

            GraphServiceClient userContextClient = await Authorization.GetGraphClientInUserContext().ConfigureAwait(false);

            // Remove self from chat
            IChatMembersCollectionPage members = await userContextClient.Chats[chatId].Members.Request().GetAsync().ConfigureAwait(false);
            ConversationMember memberToRemove = members.CurrentPage.First(member => (member as AadUserConversationMember).UserId == userId);

            await userContextClient.Chats[chatId].Members[memberToRemove.Id].Request().DeleteAsync().ConfigureAwait(false);

            var user = new TeamworkUserIdentity
            {
                Id = userId
            };

            var tenantId = ConfigurationManager.AppSettings["TenantId"];

            // Hide chat from viewpoint
            await userContextClient.Chats[chatId].HideForUser(user, tenantId).Request().PostAsync().ConfigureAwait(false);
        }
    }
}