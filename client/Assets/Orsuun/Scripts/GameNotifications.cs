using System;
using Orsuun.Rules;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif

namespace Orsuun.Client
{
    /// <summary>
    /// Local notifications (no push server and no store accounts needed): when the game goes to the background it
    /// schedules the next few reasons to come back, and clears them when the player returns. The offline hunt filling
    /// its 12 hours, the next Evening Bell, the next Commander to rise, and fresh bounties at 20:00. The permission is
    /// asked once, after the first session's oath. Phones only; other platforms do nothing.
    /// </summary>
    public sealed class GameNotifications : MonoBehaviour
    {
        private const string AskedKey = "orsuun.notifyAsked";

        private GameRoot _root;
        private bool _ready;

        public void Init(GameRoot root)
        {
            _root = root;
            _ready = false;
#if UNITY_ANDROID || UNITY_IOS
            try
            {
                NotificationCenter.Initialize(new NotificationCenterArgs
                {
                    AndroidChannelId = "orsuun",
                    AndroidChannelName = "The hunt",
                    AndroidChannelDescription = "Bells, Commanders, bounties and a full offline hunt",
                    PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Sound | NotificationPresentation.Badge,
                });
                _ready = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Notifications unavailable: " + ex.Message);
            }
#endif
        }

        /// <summary>Asks the player once whether the game may notify them.</summary>
        public void AskOnce()
        {
            if (!_ready) return;
            try
            {
                if (PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
                PlayerPrefs.SetInt(AskedKey, 1);
                PlayerPrefs.Save();
            }
            catch { return; }
#if UNITY_ANDROID || UNITY_IOS
            NotificationCenter.RequestPermission();
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_ready) return;
            if (paused) Schedule();
            else Clear();
        }

        private void Clear()
        {
#if UNITY_ANDROID || UNITY_IOS
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
            NotificationCenter.ClearBadge();
#endif
        }

        private void Schedule()
        {
            Clear();
            DateTime now = DateTime.Now;
            Add(1, "The hunt is full", "Your hero has hunted every offline hour it can hold. Come back and take the spoils.",
                now.AddSeconds(OfflineRewards.FreeCapSeconds));

            Net.ServerLink server = _root.Server;
            if (server.Online && server.Bell != null && server.Bell.minutesUntilNext > 0)
                Add(2, "The Evening Bell rings", EveningBells.Name((Bell)Enum.Parse(typeof(Bell), server.Bell.next)) + " rings on the steppe. Come and hunt it.",
                    now.AddMinutes(server.Bell.minutesUntilNext));
            else
            {
                Bell next = EveningBells.Next(now, out int minutes);
                if (next != Bell.None) Add(2, "The Evening Bell rings", EveningBells.Name(next) + " rings on the steppe. Come and hunt it.", now.AddMinutes(minutes));
            }

            // The next Commander to rise (only the soonest: one notice, not one per boss).
            if (server.Online && server.Bosses != null)
            {
                long soonest = long.MaxValue;
                string who = null;
                float age = Time.realtimeSinceStartup - server.BossesReceivedAt;
                foreach (Net.ServerLink.BossStatusDto b in server.Bosses)
                {
                    if (b.up) continue;
                    long left = b.secondsLeft - (long)age;
                    if (left > 60 && left < soonest) { soonest = left; who = b.name; }
                }
                if (who != null) Add(3, who + " rises", who + " stands on the Commander Ground. Its HP is shared by the whole server: strike before it falls.", now.AddSeconds(soonest));
            }

            if (server.Online && server.Bounties != null)
            {
                float age = Time.realtimeSinceStartup - server.BountiesReceivedAt;
                long left = server.Bounties.dailyResetSeconds - (long)age;
                if (left > 60) Add(4, "New bounties", "Fresh bounties are posted. Hunt Marks are waiting.", now.AddSeconds(left));
            }

            // The next weekend events to begin (Rules.WorldEvents), one notice each kind.
            if (server.Online)
            {
                int id = 10;
                var told = new System.Collections.Generic.HashSet<string>();
                foreach (Net.ServerLink.WorldEventDto e in server.Events)
                {
                    long wait = server.EventStartsIn(e);
                    if (wait <= 60 || !told.Add(e.kind)) continue;
                    Add(id++, e.name + " begins", $"{e.name} has begun: {e.effect}. Come and hunt it.", now.AddSeconds(wait));
                }
            }
        }

        private void Add(int id, string title, string text, DateTime when)
        {
#if UNITY_ANDROID || UNITY_IOS
            var n = new Notification { Identifier = id, Title = Loc.T(title), Text = Loc.T(text) };
            NotificationCenter.ScheduleNotification(n, new NotificationDateTimeSchedule(when));
#endif
        }
    }
}
