﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Model.Logging;
using Emby.Notifications.Discord.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Emby.Notifications.Discord
{
    public class Notifier : INotificationService
    {
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IServerConfigurationManager _serverConfiguration;
        private readonly ILibraryManager _libraryManager;
        private readonly HttpClient _httpClient;

        public List<Guid> queuedUpdateCheck = new List<Guid> { };

        public Notifier(ILogManager logManager, IJsonSerializer jsonSerializer, IServerConfigurationManager serverConfiguration, ILibraryManager libraryManager)
        {
            _logger = logManager.GetLogger(GetType().Namespace);
            _httpClient = new HttpClient();
            _jsonSerializer = jsonSerializer;
            _serverConfiguration = serverConfiguration;
            _libraryManager = libraryManager;

            _libraryManager.ItemAdded += ItemAddHandler;
            _logger.Debug("Registered ItemAdd handler");

            Thread metadataUpdateChecker = new Thread(new ThreadStart(CheckForMetadata));
            metadataUpdateChecker.Start();
        }

        public void CheckForMetadata()
        {
            do
            {
                if (queuedUpdateCheck.Count == 0) _logger.Debug("Skipping... No media pending update check");

                queuedUpdateCheck.ForEach(itemId =>
                {
                    _logger.Debug("{0} queued for recheck", itemId.ToString());

                    BaseItem item = _libraryManager.GetItemById(itemId);

                    if (item.ProviderIds.Count > 0)
                    {
                        _logger.Debug("{0}[{1}] has metadata, sending notification", item.Id, item.Name);

                        DiscordOptions options = Plugin.Instance.Configuration.Options.FirstOrDefault(opt => opt.MediaAddedOverride == true);

                        string serverName = options.ServerNameOverride ? _serverConfiguration.Configuration.ServerName : "Emby Server";

                        DiscordMessage mediaAddedEmbed = new DiscordMessage
                        {
                            embeds = new List<DiscordEmbed>()
                            {
                                new DiscordEmbed()
                                {
                                    title = $"{item.Name} ({item.ProductionYear}) has been added to {serverName}",
                                    description = item.Overview,
                                    fields = new List<Field>(),
                                    footer = new Footer
                                    {
                                        text = $"From {serverName}",
                                        icon_url = options.AvatarUrl
                                    },
                                    timestamp = DateTime.Now
                                }
                            },
                        };

                        // image must be primary
                        if(item.HasImage(ImageType.Primary))
                        {
                            mediaAddedEmbed.embeds.First().thumbnail = new Thumbnail
                            {
                                url = item.GetImagePath(ImageType.Primary)
                            };
                        }

                        item.ProviderIds.ToList().ForEach(provider =>
                        {
                            Field field = new Field
                            {
                                name = "External Details"
                            };

                            // only adding imdb and tmdb for now until further testing
                            switch(provider.Key.ToLower())
                            {
                                case "imdb":
                                    field.value = $"[IMDb](https://www.imdb.com/title/{provider.Value}/)";
                                    break;
                                case "tmdb":
                                    field.value = $"[TMDb](https://www.themoviedb.org/movie/{provider.Value})";
                                    break;
                                default:
                                    break;
                            }

                            mediaAddedEmbed.embeds.First().fields.Add(field);
                        });

                        DiscordWebhookHelper.ExecuteWebhook(mediaAddedEmbed, options.DiscordWebhookURI, _jsonSerializer, _logger, _httpClient).ConfigureAwait(false);
                        // after sending we want to remove this item from the list so it wont send the noti multiple times
                    } else
                    {
                        _logger.Debug("{0}[{1}] has no metadata", item.Id, item.Name);
                    }
                });

                Thread.Sleep(5000);
            } while (true);
        }

        public void ItemAddHandler(object sender, ItemChangeEventArgs changeEvent)
        {
            BaseItem Item = changeEvent.Item;

            // we will probably need to check for more here, im just trying to get it to work for now
            if(!Item.IsVirtualItem) {
                queuedUpdateCheck.Add(Item.Id);
            }
        }

        public bool IsEnabledForUser(User user)
        {
            DiscordOptions options = GetOptions(user);

            return options != null && !string.IsNullOrEmpty(options.DiscordWebhookURI) && options.Enabled;
        }

        private DiscordOptions GetOptions(User user)
        {
            return Plugin.Instance.Configuration.Options
                .FirstOrDefault(i => string.Equals(i.MediaBrowserUserId, user.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));
        }

        public string Name => Plugin.Instance.Name;

        public async Task SendNotification(UserNotification request, CancellationToken cancellationToken)
        {
            DiscordOptions options = GetOptions(request.User);

            string serverName = _serverConfiguration.Configuration.ServerName;

            string footerText;
            string requestName;

            if (options.ServerNameOverride)
            {
                footerText = $"From {serverName}";
                requestName = request.Name.Replace("Emby Server", serverName);
            }
            else
            {
                requestName = request.Name;
                footerText = "From Emby Server";
            }

            DiscordMessage discordMessage = new DiscordMessage
            {
                avatar_url = options.AvatarUrl,
                username = options.Username,
                embeds = new List<DiscordEmbed>()
                {
                    new DiscordEmbed()
                    {
                        color = int.Parse(options.EmbedColor.Substring(1, 6), System.Globalization.NumberStyles.HexNumber),
                        title = requestName,
                        description = request.Description,
                        footer = new Footer
                        {
                            icon_url = options.AvatarUrl,
                            text = footerText
                        },
                        timestamp = DateTime.Now
                    }
                }
            };

            switch (options.MentionType)
            {
                case MentionTypes.Everyone:
                    discordMessage.content = "@everyone";
                    break;
                case MentionTypes.Here:
                    discordMessage.content = "@here";
                    break;
            }

            await DiscordWebhookHelper.ExecuteWebhook(discordMessage, options.DiscordWebhookURI, _jsonSerializer, _logger, _httpClient);
        }
    }
}
