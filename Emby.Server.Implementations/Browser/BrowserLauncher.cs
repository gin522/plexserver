﻿using MediaBrowser.Controller;
using System;

namespace Emby.Server.Implementations.Browser
{
    /// <summary>
    /// Class BrowserLauncher
    /// </summary>
    public static class BrowserLauncher
    {
        /// <summary>
        /// Opens the dashboard page.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="appHost">The app host.</param>
        public static void OpenDashboardPage(string page, IServerApplicationHost appHost)
        {
            var url = appHost.GetLocalApiUrl("localhost") + "/web/" + page;

            OpenUrl(appHost, url);
        }

        /// <summary>
        /// Opens the community.
        /// </summary>
        public static void OpenCommunity(IServerApplicationHost appHost)
        {
            OpenUrl(appHost, "http://emby.media/community");
        }

        public static void OpenEmbyPremiere(IServerApplicationHost appHost)
        {
            OpenDashboardPage("supporterkey.html", appHost);
        }

        /// <summary>
        /// Opens the web client.
        /// </summary>
        /// <param name="appHost">The app host.</param>
        public static void OpenWebClient(IServerApplicationHost appHost)
        {
            OpenDashboardPage("index.html", appHost);
        }

        /// <summary>
        /// Opens the dashboard.
        /// </summary>
        /// <param name="appHost">The app host.</param>
        public static void OpenDashboard(IServerApplicationHost appHost)
        {
            OpenDashboardPage("dashboard.html", appHost);
        }

        /// <summary>
        /// Opens the URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        private static void OpenUrl(IServerApplicationHost appHost, string url)
        {
            try
            {
                appHost.LaunchUrl(url);
            }
            catch (NotImplementedException)
            {
                
            }
            catch (Exception)
            {
            }
        }
    }
}
