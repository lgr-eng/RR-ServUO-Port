using Server.Mobiles;
using Server.Items;
using Server.Commands;
using System.Collections.Generic;
using System.Timers;
using System;
using Server.Network;
using Server.Globals;

namespace Server.Scripts.Commands
{
    public class AutoBandSelf
    {
        private static Dictionary<Mobile, Timer> HealingTimers = new Dictionary<Mobile, Timer>();
        private static Dictionary<Mobile, bool> HealingInProgress = new Dictionary<Mobile, bool>();
        private static readonly object HealingLock = new object(); // Lock object

        public static void Initialize()
        {
            CommandSystem.Register("bandself", AccessLevel.Player, new CommandEventHandler(BandSelf_OnCommand));
            CommandSystem.Register("bs", AccessLevel.Player, new CommandEventHandler(BandSelf_OnCommand));

            // Subscribe to the Disconnected event
            EventSink.Disconnected += OnPlayerDisconnected;
        }

        private static void OnPlayerDisconnected(DisconnectedEventArgs e)
        {
            Mobile player = e.Mobile;

            if (player != null)
            {
                StopHealing(player);
            }
        }

        public class HealingTimer : Timer
        {
            private readonly Mobile _player;

            public HealingTimer(Mobile player, TimeSpan delay, TimeSpan interval)
                : base(delay, interval, 0) // 0 means infinite repetitions
            {
                _player = player;
            }

            protected override void OnTick()
            {
                AutoBandSelf.PerformHealing(_player);
            }
        }

        public static void BandSelf_OnCommand(CommandEventArgs e)
        {
            Mobile pm = e.Mobile;

            if (HealingTimers.ContainsKey(pm))
            {
                StopHealing(pm);
                pm.PrivateOverheadMessage(
                                MessageType.Regular,
                                0x3B2,
                                false,
                                $"Auto-healing has been stopped.",
                                pm.NetState
                            );
            }
            else
            {
                if (StartHealing(pm))
                {
                    pm.PrivateOverheadMessage(
                                    MessageType.Regular,
                                    0x3B2,
                                    false,
                                    $"Auto-healing has been started.",
                                    pm.NetState
                                );
                }
                else
                {
                    pm.PrivateOverheadMessage(
                                    MessageType.Regular,
                                    0x3B2,
                                    false,
                                    $"You have no bandages to start auto-healing.",
                                    pm.NetState
                                );
                }
            }
        }

        public static bool StartHealing(Mobile player)
        {
            if (player.Backpack.FindItemByType(typeof(Bandage)) == null)
            {
                return false;
            }

            TimeSpan dynamicDelay = CalculateHealingDelay(player);

            var healingTimer = new HealingTimer(player, TimeSpan.Zero, dynamicDelay)
            {
                Priority = TimerPriority.TenMS
            };

            healingTimer.Start();
            HealingTimers[player] = healingTimer;
            HealingInProgress[player] = false; // Initialize flag
            return true;
        }



        private static TimeSpan CalculateHealingDelay(Mobile healer)
        {
            double seconds = GlobalUtilities.GetHealingSeconds(healer.Dex);
            return TimeSpan.FromSeconds(seconds + 0.5);
        }

        public static void StopHealing(Mobile player)
        {
            if (HealingTimers.TryGetValue(player, out Timer healingTimer))
            {
                healingTimer.Stop();
                HealingTimers.Remove(player);
                HealingInProgress.Remove(player); // Clean up flag
            }
        }


        private static void PerformHealing(Mobile player)
        {
            lock (HealingLock) // Ensure only one thread accesses this method at a time
            {
                if (HealingInProgress.TryGetValue(player, out bool isHealing) && isHealing)
                {
                    return; // Skip if already healing
                }

                HealingInProgress[player] = true; // Mark as healing

                try
                {
                    if (player.Deleted || !player.Alive || player.Backpack == null)
                    {
                        StopHealing(player);
                        return;
                    }

                    // Check if the player's health is already full
                    if (player.Hits >= player.HitsMax)
                    {
                        return; // Exit if health is full without sending any message
                    }

                    Item bandage = player.Backpack.FindItemByType(typeof(Bandage));

                    if (bandage == null)
                    {
                        player.PrivateOverheadMessage(
                                MessageType.Regular,
                                0x3B2,
                                false,
                                $"You have run out of bandages! Auto-healing has been stopped.!",
                                player.NetState
                            );
                        StopHealing(player);
                        return;
                    }

                    if (BandageContext.BeginHeal(player, player) != null)
                    {
                        bandage.Consume();

                        if (bandage.Amount <= 20)
                        {
                            if (bandage.Amount % 2 == 0)
                            {
                                player.PrivateOverheadMessage(
                                    MessageType.Regular,
                                    0x3B2,
                                    false,
                                    $"Warning, your bandage count is currently {bandage.Amount}!",
                                    player.NetState
                                );
                            }
                        }
                    }
                    else
                    {
                        player.PrivateOverheadMessage(
                                MessageType.Regular,
                                0x3B2,
                                false,
                                $"You cannot heal at this time.",
                                player.NetState
                            );
                    }
                }
                finally
                {
                    // Ensure the flag is reset even if an exception occurs
                    HealingInProgress[player] = false;
                }
            }
        }
    }
}
