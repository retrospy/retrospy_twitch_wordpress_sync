using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Api;

namespace retrospy_twitch_wordpress_sync
{

    public class MappingAgent
    {
        private HttpClient wcHttpClient;

        public MappingAgent()
        {
            HttpMessageHandler handler = new HttpClientHandler();
            wcHttpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://retro-spy.com"),
            };
            wcHttpClient.DefaultRequestHeaders.Add("ContentType", "application/json");
            wcHttpClient.DefaultRequestHeaders.Add("User-Agent", "RetroSpy-Twitch-Sub-Mapping");
            var authBytes = System.Text.Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WPLogin") + ":" + Environment.GetEnvironmentVariable("WPAppPassword"));
            string authHeaderString = System.Convert.ToBase64String(authBytes);
            wcHttpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + authHeaderString);
        }

        private dynamic? users;
        private dynamic? userData;

        public async Task ValidateAndMoveSubscribers(TwitchAPI api)
        {
            // Get my Twitch ID
            var me = await api.Helix.Users.GetUsersAsync(new List<string>(), new List<string> { "zoggins" });

            //Gets a list of all the subscritions of the specified channel.
            var allSubscriptions = await api.Helix.Subscriptions.GetBroadcasterSubscriptionsAsync(me.Users[0].Id, 100, null);

            bool cont = false;
            int numProcessed = 0;
            int page = 1;
            do
            {
                
                HttpResponseMessage response = wcHttpClient.GetAsync("/wp-json/wc/v3/customers?per_page=100&page=" + page + "&role=all").Result;
                page++;
                string responseStr = string.Empty;

                using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                {
                    responseStr = stream.ReadToEnd();
                }

                users = JsonConvert.DeserializeObject(responseStr);
                if (users == null)
                    return;

                cont = users.Count == 100;

                foreach (var user in users)
                {
                    numProcessed++;
                    response = wcHttpClient.GetAsync("/wp-json/wp/v2/users/" + user.id + "?context=edit").Result;
                    using (StreamReader stream = new StreamReader(response.Content.ReadAsStreamAsync().Result))
                    {
                        responseStr = stream.ReadToEnd();
                    }
                    userData = JsonConvert.DeserializeObject(responseStr);
                    if (userData?.meta.twitchpress_twitch_id != string.Empty && userData?.meta.twitchpress_twitch_id != me.Users[0].Id)
                    {
                        bool noMatch = true;
                        bool update = false;
                        foreach (var sub in allSubscriptions.Data)
                        {
                            if (userData?.meta.twitchpress_twitch_id == sub.UserId)
                            {
                                userData.roles.Remove("twitchpress_role_subplan_1000");
                                userData.roles.Remove("twitchpress_role_subplan_2000");
                                userData.roles.Remove("twitchpress_role_subplan_3000");

                                if (sub.Tier == "1000")
                                {
                                    userData.roles.Add("twitchpress_role_subplan_1000");
                                }
                                else if (sub.Tier == "2000")
                                {
                                    userData.roles.Add("twitchpress_role_subplan_2000");
                                }
                                else if (sub.Tier == "3000")
                                {
                                    userData.roles.Add("twitchpress_role_subplan_3000");
                                }
                                noMatch = false;
                                update = true;
                                break;
                            }
                        }

                        if (noMatch &&
                            (userData?.roles.Contains("twitchpress_role_subplan_1000")
                            || userData?.roles.Contains("twitchpress_role_subplan_2000")
                            || userData?.roles.Contains("twitchpress_role_subplan_3000")))
                        {
                            userData?.roles.Remove("twitchpress_role_subplan_1000");
                            userData?.roles.Remove("twitchpress_role_subplan_2000");
                            userData?.roles.Remove("twitchpress_role_subplan_3000");
                            update = true;
                        }

                        if (update)
                        {
                            var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
                            s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
                        }
                    }
                    else
                    {
                        if (userData?.roles.Contains("twitchpress_role_subplan_1000")
                           ||userData?.roles.Contains("twitchpress_role_subplan_2000")
                           || userData?.roles.Contains("twitchpress_role_subplan_3000"))
                        {
                            userData?.roles.Remove("twitchpress_role_subplan_1000");
                            userData?.roles.Remove("twitchpress_role_subplan_2000");
                            userData?.roles.Remove("twitchpress_role_subplan_3000");

                            var s = new StringContent("{\n \"roles\": " + userData?.roles.ToString() + "\n}");
                            s.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            response = wcHttpClient.PutAsync("/wp-json/wp/v2/users/" + user.id, s).Result;
                        }
                    }
                }
            } while (cont);

        }
    }
}
