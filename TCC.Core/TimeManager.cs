﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TCC.Data;
using TCC.Parsing;
using TCC.ViewModels;

namespace TCC
{
    public class TimeManager : TSPropertyChanged
    {
        private readonly Dictionary<string, TeraServerTimeInfo> _serverTimezones = new Dictionary<string, TeraServerTimeInfo>
        {
            {"EU", new TeraServerTimeInfo("Central Europe Standard Time", 6, DayOfWeek.Wednesday) },
            {"NA", new TeraServerTimeInfo("Central Standard Time", 6, DayOfWeek.Tuesday) },
            {"RU", new TeraServerTimeInfo("Russian Standard Time", 6, DayOfWeek.Wednesday) },
            {"TW", new TeraServerTimeInfo("China Standard Time", 6, DayOfWeek.Wednesday) },
            {"JP", new TeraServerTimeInfo("Tokyo Standard Time", 6, DayOfWeek.Wednesday) },
            {"KR", new TeraServerTimeInfo("Korea Standard Time", 6, DayOfWeek.Wednesday) }
        };

        public const double SecondsInDay = 60 * 60 * 24;
        private const string BaseUrl = "https://tcc-web-99a64.firebaseapp.com/bam";

        private static TimeManager _instance;
        private DayOfWeek _resetDay;
        public int ResetHour;
        public static TimeManager Instance => _instance ?? (_instance = new TimeManager());
        public string CurrentRegion { get; set; }
        public int ServerHourOffsetFromLocal;
        public int ServerHourOffsetFromUtc;

        public DateTime CurrentServerTime => DateTime.Now.AddHours(ServerHourOffsetFromLocal);

        private TimeManager()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            var s = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            s.Tick += CheckNewDay;
            s.Start();
        }

        private void CheckCloseEvents()
        {
            var closeEventsCount = InfoWindowViewModel.Instance.EventGroups.Count(evGroup => evGroup.Events.Any(x => x.IsClose));
            if (closeEventsCount == 0) return;
            WindowManager.FloatingButton.StartNotifying(closeEventsCount);

        }

        private void CheckNewDay(object sender, EventArgs e)
        {
            if (CurrentServerTime.Hour == 0 && CurrentServerTime.Minute== 0)
                InfoWindowViewModel.Instance.LoadEvents(CurrentServerTime.DayOfWeek, CurrentRegion);
            if (CurrentServerTime.Second == 0 && CurrentServerTime.Minute % 3 == 0) CheckCloseEvents();
        }

        public void SetServerTimeZone(string region)
        {
            if (string.IsNullOrEmpty(region)) return;
            CurrentRegion = region.StartsWith("EU") ? "EU" : region;

            SettingsManager.LastRegion = CurrentRegion;

            var timezone = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(x => x.Id == _serverTimezones[CurrentRegion].Timezone);
            ResetHour = _serverTimezones[CurrentRegion].ResetHour;
            _resetDay = _serverTimezones[CurrentRegion].ResetDay;

            if (timezone != null)
            {
                ServerHourOffsetFromUtc = timezone.IsDaylightSavingTime(DateTime.UtcNow + timezone.BaseUtcOffset)
                    ? timezone.BaseUtcOffset.Hours + 1
                    : timezone.BaseUtcOffset.Hours;
                ServerHourOffsetFromLocal = -TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Hours + ServerHourOffsetFromUtc;
            }

            if (InfoWindowViewModel.Instance.Markers.FirstOrDefault(x => x.Name.Equals(CurrentRegion + " server time")) == null)
            {
                InfoWindowViewModel.Instance.Markers.Add(new TimeMarker(ServerHourOffsetFromLocal, CurrentRegion + " server time"));
            }

            CheckReset();
            InfoWindowViewModel.Instance.LoadEvents(DateTime.Now.DayOfWeek, CurrentRegion);

        }

        private void CheckReset()
        {
            if (CurrentRegion == null) return;
            var todayReset = DateTime.Today.AddHours(ResetHour + ServerHourOffsetFromLocal);
            if(SettingsManager.LastRun > todayReset || DateTime.Now < todayReset) return;
            foreach (var ch in InfoWindowViewModel.Instance.Characters)
            {
                foreach (var dg in ch.Dungeons)
                {
                    if (dg.Id == 9950)
                    {
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Thursday) dg.Reset();
                        else continue;
                    }
                    dg.Reset();
                }
                ch.DailiesDone = 0;

                if (DateTime.Now.DayOfWeek == _resetDay)
                {
                    ch.WeekliesDone = 0;
                }
            }
            SettingsManager.LastRun = DateTime.Now;
            InfoWindowViewModel.Instance.SaveToFile();
            SettingsManager.SaveSettings();
            if (DateTime.Now.DayOfWeek == _resetDay)
            {
                ChatWindowViewModel.Instance.AddTccMessage("Weekly data has been reset.");
            }

            ChatWindowViewModel.Instance.AddTccMessage("Daily data has been reset.");
        }


        private async Task<long> DownloadGuildBamTimestamp()
        {
            var sb = new StringBuilder(BaseUrl);
            sb.Append("?srv=");
            sb.Append(PacketProcessor.ServerId);
            sb.Append("&reg=");
            sb.Append(CurrentRegion);
            var c = new WebClient();
            try
            {
                var data = await c.DownloadStringTaskAsync(sb.ToString());
                return long.Parse(data);

            }
            catch
            {
                ChatWindowViewModel.Instance.AddTccMessage("Failed to retrieve guild bam info.");
                return 0;
            }
        }

        public void UploadGuildBamTimestamp()
        {
            var ts = CurrentServerTime - new DateTime(1970, 1, 1);
            var time = (long)ts.TotalSeconds;
            var sb = new StringBuilder(BaseUrl);
            sb.Append("?srv=");
            sb.Append(PacketProcessor.ServerId);
            sb.Append("&reg=");
            sb.Append(CurrentRegion);
            sb.Append("&post");
            var c = new WebClient();
            try
            {
                c.UploadDataAsync(new Uri(sb.ToString()), new byte[] { });
            }
            catch 
            {
                ChatWindowViewModel.Instance.AddTccMessage("Failed to upload guild bam info.");   
            }

        }
        public DateTime RetrieveGuildBamDateTime()
        {
            var t = DownloadGuildBamTimestamp();
            t.Wait();
            var ts = t.Result;
            return Utils.FromUnixTime(ts);
        }

        public void SetGuildBamTime(bool force)
        {
            foreach (var eg in InfoWindowViewModel.Instance.EventGroups.Where(x => x.RemoteCheck))
            {
                foreach (var ev in eg.Events)
                {
                    ev.UpdateFromServer(force);
                }
            }
        }

        public void SendWebhookMessageOld()
        {
            if (!String.IsNullOrEmpty(SettingsManager.Webhook))
            {
                var sb = new StringBuilder("{");
                sb.Append("\"");sb.Append("content");sb.Append("\"");
                sb.Append(":");
                sb.Append("\"");sb.Append(SettingsManager.WebhookMessage);sb.Append("\"");
                sb.Append(",");
                sb.Append("\""); sb.Append("username"); sb.Append("\"");
                sb.Append(":");
                sb.Append("\""); sb.Append("TCC"); sb.Append("\"");
                sb.Append(",");
                sb.Append("\""); sb.Append("avatar_url"); sb.Append("\"");
                sb.Append(":");
                sb.Append("\""); sb.Append("http://i.imgur.com/8IltuVz.png"); sb.Append("\"");
                sb.Append("}");

                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("Content-Type", "application/json");
                        var resp = client.UploadString(SettingsManager.Webhook, "POST", sb.ToString());
                    }
                }
                catch (Exception)
                {
                    ChatWindowViewModel.Instance.AddTccMessage("Failed to execute Discord webhook.");
                }
            }
        }
        public void SendWebhookMessage(string bamName)
        {
            if (!String.IsNullOrEmpty(SettingsManager.Webhook))
            {
                var msg = SettingsManager.WebhookMessage.IndexOf("{npc_name}", StringComparison.Ordinal) > -1
                    ? SettingsManager.WebhookMessage.Replace("{npc_name}", bamName)
                    : SettingsManager.WebhookMessage;
                var sb = new StringBuilder("{");
                sb.Append("\""); sb.Append("content"); sb.Append("\"");
                sb.Append(":");
                sb.Append("\""); sb.Append(msg); sb.Append("\"");
                sb.Append(",");
                sb.Append("\""); sb.Append("username"); sb.Append("\"");
                sb.Append(":");
                sb.Append("\""); sb.Append("TCC"); sb.Append("\"");
                sb.Append(",");
                sb.Append("\""); sb.Append("avatar_url"); sb.Append("\"");
                sb.Append(":");
                sb.Append("\""); sb.Append("http://i.imgur.com/8IltuVz.png"); sb.Append("\"");
                sb.Append("}");

                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("Content-Type", "application/json");
                        var resp = client.UploadString(SettingsManager.Webhook, "POST", sb.ToString());
                    }
                }
                catch (Exception)
                {
                    ChatWindowViewModel.Instance.AddTccMessage("Failed to execute Discord webhook.");
                }
            }
        }

    }
}
