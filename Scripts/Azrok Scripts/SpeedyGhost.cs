// SpeedyGhost v1.0 code thanks to zerodowned and PoOka
// Sets player speed to mount speed upon death
using System;
using System.Collections;
using Server;
using Server.Gumps;
using Server.Network;
using Server.Mobiles;
using Server.Accounting;
using Server.Commands;

namespace Ixtabay.SpeedyGhost
{
    public class SpeedyGhost_Main
    {
        public static void Initialize()
        {
            EventSink.PlayerDeath += new PlayerDeathEventHandler(EventSink_Death);
        }
        private static void EventSink_Death(PlayerDeathEventArgs e)
        {
            if (e.Mobile != null)
            {
                if (!e.Mobile.Alive)
                    e.Mobile.Send(SpeedControl.MountSpeed);
            }
        }
    }
}
