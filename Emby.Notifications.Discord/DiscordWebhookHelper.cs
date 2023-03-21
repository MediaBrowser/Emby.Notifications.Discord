﻿using System;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;

namespace Emby.Notifications.Discord
{
    public class DiscordEmbed
    {
        public int color { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string description { get; set; }
        public Thumbnail thumbnail { get; set; }
        public List<Field> fields { get; set; }
        public Footer footer { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class Thumbnail
    {
        public string url { get; set; }
    }

    public class Field
    {
        public string name { get; set; }
        public string value { get; set; }
        public Boolean inline { get; set; }
    }

    public class Footer
    {
        public string text { get; set; }
        public string icon_url { get; set; }
    }

    public class DiscordMessage
    {
        public string avatar_url { get; set; }
        public string username { get; set; }
        public string content { get; set; }
        public List<DiscordEmbed> embeds { get; set; }
    }

    public class DiscordWebhookHelper
    {
        public static int FormatColorCode(string hexCode)
        {
            return int.Parse(hexCode.Substring(1, 6), System.Globalization.NumberStyles.HexNumber);
        }

        public static async Task ExecuteWebhook(DiscordMessage message, string webhookUrl, IJsonSerializer _jsonSerializer, IHttpClient httpClient, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(_jsonSerializer.SerializeToString(message));

            var httpRequestOptions = new HttpRequestOptions
            {
                Url = webhookUrl,
                CancellationToken = cancellationToken
            };

            httpRequestOptions.RequestContentBytes = bytes;
            httpRequestOptions.RequestContentType = "application/json";

            using (await httpClient.Post(httpRequestOptions).ConfigureAwait(false))
            {

            }
        }
    }
}
