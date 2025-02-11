using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Globals
{
    public static class GlobalUtilities
    {
        /// <summary>
        /// Calculates the healing seconds based on the given dexterity.
        /// </summary>
        /// <param name="dex">Dexterity of the character (clamped between 10 and 125).</param>
        /// <returns>Healing delay in seconds.</returns>
        public static double GetHealingSeconds(int dex)
        {
            dex = Math.Min(dex, 125); // Clamp dexterity to a maximum of 125
            if (dex <= 45)
            {
                return 7.857 - (3.0 / 35.0) * dex; // For dexterity between 10 and 45
            }
            else
            {
                return 5.125 - (1.0 / 40.0) * dex; // For dexterity between 45 and 125
            }
        }
        public static int getLMCValue(Mobile m)
        {
            int lmc = Math.Min(AosAttributes.GetValue(m, AosAttribute.LowerManaCost), 40);
            return lmc;
        }
    }
}

