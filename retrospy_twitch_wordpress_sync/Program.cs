﻿using retrospy_twitch_wordpress_sync;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using static System.Formats.Asn1.AsnWriter;

namespace retrospy_twitch_wordpress_sync
{
    class Program
    {
        private static readonly List<string> scopes = ["channel:read:subscriptions"];

        private static TwitchAPI? api;

        private static void ValidateCreds()
        {
            if (String.IsNullOrEmpty(Config.TwitchClientId))
                throw new Exception("client id cannot be null or empty");
            if (String.IsNullOrEmpty(Config.TwitchClientSecret))
                throw new Exception("client secret cannot be null or empty");
            if (String.IsNullOrEmpty(Config.TwitchRedirectUri))
                throw new Exception("redirect uri cannot be null or empty");
            Console.WriteLine($"Using client id '{Config.TwitchClientId}', secret '{Config.TwitchClientSecret}' and redirect url '{Config.TwitchRedirectUri}'.");
        }

        private static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();

            MappingAgent wp = new();
            if (api != null)
                wp.ValidateAndMoveSubscribers(api).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args)
        {
            Config.TwitchClientSecret = Environment.GetEnvironmentVariable("TwitchClientSecret") ?? string.Empty;
            Config.TwitchRedirectUri = Environment.GetEnvironmentVariable("TwitchRedirectUri") ?? string.Empty;
            Config.TwitchClientId = Environment.GetEnvironmentVariable("TwitchClientId") ?? string.Empty;

            // ensure client id, secret, and redrect url are set
            ValidateCreds();

            // create twitch api instance
            api = new TwitchAPI();
            api.Settings.ClientId = Config.TwitchClientId;

            RefreshResponse refresh;
            User? user;
            if (args.Length == 1 && args[0] == "--auth")
            {
                // start local web server
                var server = new WebServer(Config.TwitchRedirectUri);

                // print out auth url
                Console.WriteLine($"Please authorize here:\n{getAuthorizationCodeUrl(Config.TwitchClientId, Config.TwitchRedirectUri, scopes)}");

                // listen for incoming requests
                var auth = await server.Listen();

                // exchange auth code for oauth access/refresh
                var resp = await api.Auth.GetAccessTokenFromCodeAsync(auth.Code, Config.TwitchClientSecret, Config.TwitchRedirectUri);

                // update TwitchLib's api with the recently acquired access token
                api.Settings.AccessToken = resp.AccessToken;

                // get the auth'd user
                user = (await api.Helix.Users.GetUsersAsync()).Users[0];

                // print out all the data we've got
                Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}");

                // refresh token
                refresh = await api.Auth.RefreshAuthTokenAsync(resp.RefreshToken, Config.TwitchClientSecret);
                api.Settings.AccessToken = refresh.AccessToken;
                Environment.SetEnvironmentVariable("WPRefreshToken", refresh.RefreshToken, EnvironmentVariableTarget.User);

                // confirm new token works
                user = (await api.Helix.Users.GetUsersAsync()).Users[0];

                // print out all the data we've got
                Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {refresh.AccessToken}\nRefresh token: {refresh.RefreshToken}\nExpires in: {refresh.ExpiresIn}\nScopes: {string.Join(", ", refresh.Scopes)}");

                refresh = await api.Auth.RefreshAuthTokenAsync(refresh.RefreshToken, Config.TwitchClientSecret);

                api.Settings.AccessToken = refresh.AccessToken;

                // confirm new token works
                user = (await api.Helix.Users.GetUsersAsync()).Users[0];

                // print out all the data we've got
                Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {refresh.AccessToken}\nRefresh token: {refresh.RefreshToken}\nExpires in: {refresh.ExpiresIn}\nScopes: {string.Join(", ", refresh.Scopes)}");
            }
            else
            {
                refresh = await api.Auth.RefreshAuthTokenAsync(Environment.GetEnvironmentVariable("WPRefreshToken"), Config.TwitchClientSecret);
                api.Settings.AccessToken = refresh.AccessToken;
                Environment.SetEnvironmentVariable("WPRefreshToken", refresh.RefreshToken, EnvironmentVariableTarget.User);

                // confirm new token works
                user = (await api.Helix.Users.GetUsersAsync()).Users[0];

                // print out all the data we've got
                Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {refresh.AccessToken}\nRefresh token: {refresh.RefreshToken}\nExpires in: {refresh.ExpiresIn}\nScopes: {string.Join(", ", refresh.Scopes)}");
            }
        }

        private static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);

            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   "response_type=code&" +
                   $"scope={scopesStr}";
        }
    }
}