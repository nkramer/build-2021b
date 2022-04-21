namespace Microsoft.Teams.Samples.HelloWorld.Web.Controllers
{
    using System;
    using System.Configuration;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;

    [Route("messages")]
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] Activity activity)
        {
            using (var connector = new ConnectorClient(new Uri(activity.ServiceUrl), this.BotAppId, this.BotAppPassword))
            {
                if (activity.IsComposeExtensionQuery())
                {
                    var response = MessageExtension.HandleMessageExtensionQuery(connector, activity);
                    return response != null
                        ? Request.CreateResponse<ComposeExtensionResponse>(response)
                        : new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    await CTradeBot.ProcessMessage(connector, activity);
                    return new HttpResponseMessage(HttpStatusCode.Accepted);
                }
            }
        }

        private string BotAppId => ConfigurationManager.AppSettings["MicrosoftAppId"];
        private string BotAppPassword => ConfigurationManager.AppSettings["MicrosoftAppPassword"];
    }
}
