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
        private SocketGuild _guild;
        private DiscordSocketClient _client;
        private System.Timers.Timer _timer;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _timer = new System.Timers.Timer(30000);
            _client = new DiscordSocketClient();
            _client.Log += Log;

            string token = "nope";
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
            _client.GuildMemberUpdated += UserUpdated;
            _timer.Elapsed += heartbeat;

            _timer.Enabled = true;
            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private void heartbeat(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("Dyno is still alive.");
        }

        private async Task UserUpdated(SocketUser before, SocketUser after)
        {
            var user = after as SocketGuildUser;
            if (authGrant(user.VoiceChannel) && user.VoiceChannel != null)
            {
                await refreshChannel(user.VoiceChannel);
            }
        }

        private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if (authGrant(after.VoiceChannel) && authGrant(before.VoiceChannel)
                && after.VoiceChannel != null && before.VoiceChannel != null)
            {
                Task.WaitAll(refreshChannel(after.VoiceChannel), refreshChannel(before.VoiceChannel));
            }
            else if (authGrant(after.VoiceChannel) && after.VoiceChannel != null)
            {
                await refreshChannel(after.VoiceChannel);
            }
            else if (authGrant(before.VoiceChannel) && before.VoiceChannel != null)
            {
                await refreshChannel(before.VoiceChannel);
            }
            else
            {
                // do nothing
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private bool authGrant(SocketVoiceChannel channel)
        {
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }

        private async Task refreshChannel(SocketVoiceChannel channel)
        {
            var guild = _client.GetGuild(channel.Guild.Id);
            var guildChannel = guild.GetChannel(channel.Id);

            // TODO: check all users, if no one in game, set channel to default

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
    }
}
