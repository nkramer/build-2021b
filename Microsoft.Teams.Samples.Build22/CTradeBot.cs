namespace Microsoft.Teams.Samples.HelloWorld.Web
{
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Graph;
    using Microsoft.Teams.Samples.HelloWorld.Web.Controllers;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;


    public class CTradeBot
    {
        public static async Task ProcessMessage(ConnectorClient connector, Bot.Connector.Activity activity)
        {
            // Initial chat opening usually sends null input, so ignore and don't echo.
            if (string.IsNullOrEmpty(activity.Text))
            {
                return;
            }

            if (activity is IMessageActivity)
            {
                if (activity.GetTextWithoutMentions() != "")
                {
                    string input = activity.Text;
                    input = input.Replace("<at>cTrade</at> ", "");
                    string[] words = input.Split(' ');
                    words[0] = words[0].ToLower();

                    string reply;
                    if (words[0].StartsWith("meet"))
                    {
                        DateTimeOffset meetingStartTime = DateTimeOffset.Parse(words[1]);
                        string chatId = activity.Conversation.Id;

                        var actionResponse = activity.CreateReply($"Ok, scheduling a meeting for {words[1]}...");
                        await connector.Conversations.ReplyToActivityWithRetriesAsync(actionResponse);

                        await ScheduleMeeting(chatId, meetingStartTime).ConfigureAwait(false);
                        reply = $"Meeting scheduled for {words[1]}.";
                    }
                    else if (words[0].StartsWith("buy"))
                    {
                        reply = $"Bought {words[1]} shares.";
                    }
                    else
                    {
                        reply = "Huh?";
                    }

                    var response = activity.CreateReply(reply);
                    await connector.Conversations.ReplyToActivityWithRetriesAsync(response);
                }
            }
        }

        private static async Task ScheduleMeeting(string chatId, DateTimeOffset meetingStartTime)
        {
            GraphServiceClient userContextClient = await Authorization.GetGraphClientInUserContext().ConfigureAwait(false);

            // Get members of the chat where bot is located
            Task<IChatMembersCollectionPage> getChatMembersTask = userContextClient.Chats[chatId].Members.Request().GetAsync();

            Event newEvent = new Event
            {
                Subject = "Discuss FBKM Stock",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = "I need some advice on the FBKM stock."
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = meetingStartTime.DateTime.ToString(),
                    TimeZone = "Pacific Standard Time"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = meetingStartTime.AddHours(1).DateTime.ToString(),
                    TimeZone = "Pacific Standard Time"
                },
                IsOnlineMeeting = true
            };

            IChatMembersCollectionPage chatMembers = await getChatMembersTask.ConfigureAwait(false);

            List<Attendee> eventAttendees = new List<Attendee>();

            // Add those members to the event attendees list.
            foreach (ConversationMember member in chatMembers.CurrentPage)
            {
                AadUserConversationMember aadUserConversationMember = member as AadUserConversationMember;

                eventAttendees.Add(new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = aadUserConversationMember.Email,
                        Name = aadUserConversationMember.DisplayName,
                    },
                    Type = AttendeeType.Required
                });
            }

            newEvent.Attendees = eventAttendees;

            await userContextClient.Me.Calendar.Events.Request().AddAsync(newEvent).ConfigureAwait(false);
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
