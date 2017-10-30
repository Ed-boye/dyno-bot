using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Dynobot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private System.Timers.Timer _timer;
        private StringBuilder connectedGuilds;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _timer = new System.Timers.Timer(30000);
            _client = new DiscordSocketClient();
            connectedGuilds = new StringBuilder();

            string token = "Mzc0MzEyMzEwMzM0MDI5ODM1.DNfdBg.ar7jV-azPUUptyyes_DjMP1T1U8";
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Log += Log;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;
            _client.GuildMemberUpdated += UserUpdatedAsync;
            _client.JoinedGuild += UpdateGuildList; ;
            _client.LeftGuild += UpdateGuildList;
            _client.Ready += Init;
            _timer.Elapsed += heartbeat;

            _timer.Enabled = true;
            
            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Init()
        {
            Console.WriteLine("Connected to " + _client.Guilds.Count + " guild(s).");
            foreach(SocketGuild guild in _client.Guilds)
            {
                connectedGuilds.Append(guild.Name + ", ");

                foreach (SocketGuildChannel channel in guild.Channels)
                {
                    var voiceChannel = channel as SocketVoiceChannel;
                    if (voiceChannel != null && authGrant(voiceChannel))
                    {
                        Task.Run(() => refreshChannelAsync(voiceChannel)).Wait();
                    }
                }
            }

            connectedGuilds.Remove(connectedGuilds.Length - 2, 2);

            return Task.CompletedTask;
        }

        private Task UpdateGuildList(SocketGuild unused)
        {
            Console.WriteLine("Guild status updated, connected to " + _client.Guilds.Count + " guild(s).");
            connectedGuilds.Clear();
            foreach (SocketGuild guild in _client.Guilds)
            {
                connectedGuilds.Append(guild.Name + ", ");
            }
            connectedGuilds.Remove(connectedGuilds.Length - 2, 2);
            return Task.CompletedTask;
        }

        private async Task UserUpdatedAsync(SocketUser before, SocketUser after)
        {
            var user = after as SocketGuildUser;
            if (authGrant(user.VoiceChannel) && user.VoiceChannel != null)
            {
                await refreshChannelAsync(user.VoiceChannel);
            }
        }

        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            await Task.WhenAll(checkAndUpdateAsync(after.VoiceChannel), checkAndUpdateAsync(before.VoiceChannel));
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task checkAndUpdateAsync(SocketVoiceChannel voiceChannel)
        {
            if (voiceChannel != null && authGrant(voiceChannel))
            {
                await refreshChannelAsync(voiceChannel);
            }
        }

        // add auth decorator
        private async Task refreshChannelAsync(SocketVoiceChannel channel)
        {
            var guild = _client.GetGuild(channel.Guild.Id);
            var guildChannel = guild.GetChannel(channel.Id);

            //if only user
            if (channel.Users.Count == 1 && channel.Users.First().Game != null)
            {
                await guildChannel.ModifyAsync(x => x.Name = channel.Users.First().Game.Value.Name);
            }
            else if (channel.Users.Count > 1)
            {
                var gamesDict = new Dictionary<String, int>();
                //string most = "";
                foreach (SocketGuildUser person in channel.Users)
                {
                    if (person.Game != null)
                    {
                        String gameName = person.Game.Value.Name;
                        int count;
                        if (gamesDict.TryGetValue(gameName, out count))
                        {
                            gamesDict[gameName]++;
                        }
                        else
                        {
                            gamesDict.Add(person.Game.Value.Name, 1);
                        }
                    }
                }

                var list = gamesDict.ToList();
                if (list.Count == 0)
                {
                    await guildChannel.ModifyAsync(x => x.Name = "Channel " + channel.Position + " (Dynamic)");
                    return;
                }
                else if (list.Count == 1)
                {
                    await guildChannel.ModifyAsync(x => x.Name = list.First().Key);
                }
                else // list.Count > 1
                {
                    list.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                    list.Reverse();
                    if (list.ElementAt(0).Value == list.ElementAt(1).Value)
                    {
                        // Tied Games get don't change channel name
                        return;
                    }
                    await guildChannel.ModifyAsync(x => x.Name = list.First().Key);
                }
            }
            else
            {
                await guildChannel.ModifyAsync(x => x.Name = "Channel " + channel.Position + " (Dynamic)");
            }
        }

        private bool authGrant(SocketVoiceChannel channel)
        {
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }

        private void heartbeat(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("Connected to the following guild(s): " + connectedGuilds.ToString());
        }
    }
}
