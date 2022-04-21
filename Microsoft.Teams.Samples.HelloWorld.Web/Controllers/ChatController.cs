namespace Microsoft.Teams.Samples.HelloWorld.Web.Controllers
{
    using Microsoft.Graph;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
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
        public async Task CreateChat()
        {
            string currentToken = "eyJ0eXAiOiJKV1QiLCJub25jZSI6IlpFdFRIRWFhNWdjS3hFamE3V2cyckpyT2xrbWFCSWNkeUZDVjVYN0NLOHciLCJhbGciOiJSUzI1NiIsIng1dCI6ImpTMVhvMU9XRGpfNTJ2YndHTmd2UU8yVnpNYyIsImtpZCI6ImpTMVhvMU9XRGpfNTJ2YndHTmd2UU8yVnpNYyJ9.eyJhdWQiOiJodHRwczovL2dyYXBoLm1pY3Jvc29mdC5jb20iLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC8yNDMyYjU3Yi0wYWJkLTQzZGItYWE3Yi0xNmVhZGQxMTVkMzQvIiwiaWF0IjoxNjUwMzIxNjc5LCJuYmYiOjE2NTAzMjE2NzksImV4cCI6MTY1MDMyNjg5NiwiYWNjdCI6MCwiYWNyIjoiMSIsImFpbyI6IkFTUUEyLzhUQUFBQUU5eXQzTkZlMHpqeWxMTHV2dy9MYmhWWnNkdkNSdmxpWmF1QndHbmx4UUk9IiwiYW1yIjpbInB3ZCJdLCJhcHBfZGlzcGxheW5hbWUiOiJBRk4gRGVtbyIsImFwcGlkIjoiY2Y3YWFkYTMtOGNiNi00NWEzLTg4OGQtYmNlZjRmOWJmNzJjIiwiYXBwaWRhY3IiOiIxIiwiZmFtaWx5X25hbWUiOiJSYXkiLCJnaXZlbl9uYW1lIjoiU3ViaGFqaXQiLCJpZHR5cCI6InVzZXIiLCJpcGFkZHIiOiIyNC41Ni4yNDMuOCIsIm5hbWUiOiJTdWJoYWppdCBSYXkiLCJvaWQiOiI4MmZlNzc1OC01YmIzLTRmMGQtYTQzZi1lNTU1ZmQzOTljNmYiLCJwbGF0ZiI6IjE0IiwicHVpZCI6IjEwMDMyMDAwQjRERDE2RDAiLCJyaCI6IjAuQVZjQWU3VXlKTDBLMjBPcWV4YnEzUkZkTkFNQUFBQUFBQUFBd0FBQUFBQUFBQUJYQUJBLiIsInNjcCI6IkFwcENhdGFsb2cuUmVhZFdyaXRlLkFsbCBDaGFubmVsLkNyZWF0ZSBDaGFubmVsLlJlYWRCYXNpYy5BbGwgQ2hhbm5lbE1lc3NhZ2UuUmVhZC5BbGwgQ2hhbm5lbE1lc3NhZ2UuU2VuZCBDaGF0LkNyZWF0ZSBDaGF0LlJlYWRXcml0ZSBNYWlsLlJlYWQgVGVhbS5DcmVhdGUgVGVhbS5SZWFkQmFzaWMuQWxsIFRlYW1NZW1iZXIuUmVhZC5BbGwgVGVhbXNBY3Rpdml0eS5TZW5kIFRlYW1zQXBwSW5zdGFsbGF0aW9uLlJlYWRGb3JDaGF0IFRlYW1zQXBwSW5zdGFsbGF0aW9uLlJlYWRGb3JUZWFtIFRlYW1zQXBwSW5zdGFsbGF0aW9uLlJlYWRGb3JVc2VyIFRlYW1zQXBwSW5zdGFsbGF0aW9uLlJlYWRXcml0ZVNlbGZGb3JVc2VyIFRlYW13b3JrVGFnLlJlYWRXcml0ZSBVc2VyLlJlYWQgcHJvZmlsZSBvcGVuaWQgZW1haWwiLCJzdWIiOiJJMlczS2tNOVNMSnplaFBVdVFfalNNTDBUTnNyclAxV196amtJZk5mZnBBIiwidGVuYW50X3JlZ2lvbl9zY29wZSI6Ik5BIiwidGlkIjoiMjQzMmI1N2ItMGFiZC00M2RiLWFhN2ItMTZlYWRkMTE1ZDM0IiwidW5pcXVlX25hbWUiOiJzdWJyYXlAdGVhbXNncmFwaC5vbm1pY3Jvc29mdC5jb20iLCJ1cG4iOiJzdWJyYXlAdGVhbXNncmFwaC5vbm1pY3Jvc29mdC5jb20iLCJ1dGkiOiJLUFh0cWZjemtrLWN2cEROR2pkUkFBIiwidmVyIjoiMS4wIiwid2lkcyI6WyI2MmU5MDM5NC02OWY1LTQyMzctOTE5MC0wMTIxNzcxNDVlMTAiLCI2OTA5MTI0Ni0yMGU4LTRhNTYtYWE0ZC0wNjYwNzViMmE3YTgiLCJmMmVmOTkyYy0zYWZiLTQ2YjktYjdjZi1hMTI2ZWU3NGM0NTEiLCJmZTkzMGJlNy01ZTYyLTQ3ZGItOTFhZi05OGMzYTQ5YTM4YjEiLCJiNzlmYmY0ZC0zZWY5LTQ2ODktODE0My03NmIxOTRlODU1MDkiXSwieG1zX3N0Ijp7InN1YiI6IklWc2FPQzFaN21ucFRhSzM1S1NkcU1heVN3MkdxeHB4MG5STFUyN0dvVWcifSwieG1zX3RjZHQiOjE1NzcxNDI3NDZ9.nG0Q_Pr7KUHhNgXcCSia7OsrbS8ZFjZu-Dw5LTWaF6MsfiYoyBOYvVSGBhvO9NTibeYqaVCrmAueKpdbMK0A9AbLs209i1455owjTwgHEiGr2Pp-RtWpuo704zmv3t9inU9u125qpQRkf7HXJeVZfuF5ZxhodOtrXSHNaW0JbUYjWlzLbS1cC2UhsCdp8OrkxWSHacHze1TJB56RGIypKwtD1FY7ShbuDH0vxT8p0ZAwzLazpTjPD2czTNJMbmUi5-A5QG3wpUxVBu5Jz3hzhd0yDCr34202PzGpQHhHHtUdlVtZsLjgH8J8BJ1Zi6UkOWIojBNSVMH4gEKOH0YvTA";
            
            GraphServiceClient client = await Authorization.GetGraphClient(currentToken).ConfigureAwait(false);

            Chat newChat = new Chat
            {
                ChatType = ChatType.Group,
                Topic = "Demo Chat from VS",
                Members = new List<ConversationMember>
                {
                    new AadUserConversationMember
                    {
                        UserId = "c27c1b19-3904-4822-9813-4f6bdaab2eae",
                        Roles = new List<string> { "owner" },
                    },
                    new AadUserConversationMember
                    {
                        UserId = "4b822dfc-2864-44e6-aa1e-7e0e8552168f",
                        Roles = new List<string> { "owner" },
                    },
                    new AadUserConversationMember
                    {
                        UserId = "82fe7758-5bb3-4f0d-a43f-e555fd399c6f",
                        Roles = new List<string> { "owner" },
                    },
                    new AadUserConversationMember
                    {
                        UserId = "70292a90-d2a7-432c-857e-55db6d8f5cd0",
                        TenantId = "139d16b4-7223-43ad-b9a8-674ba63c7924",
                        Roles = new List<string> { "owner" },
                    },
                } as IChatMembersCollectionPage,
            };

            Chat createdChat = await client.Chats.Request().AddAsync(newChat).ConfigureAwait(false);
            Console.WriteLine(createdChat.Id);
        }
    }
}