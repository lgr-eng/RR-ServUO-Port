using Server;
using Server.Items;
using System;

namespace Bittiez.CustomLoot
{
    public static class CustomLootSystem
    {
        private static Random _random = new Random();
        public static void Initialize()
        {
            Console.WriteLine("Loot System Initialize");
            EventSink.CreatureDeath += EventSink_CreatureDeath;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void EventSink_CreatureDeath(CreatureDeathEventArgs e)
        {
            Console.WriteLine("Loot System EventSink_CreatureDeath");
            if (e.Creature == null) return;

            Mobile creature = e.Creature;
            Mobile killer = e.Killer;

            // Scaling probability based on fame
            int fame = creature.Fame; // Assuming fame is a property of the creature

            // Sum the creature's stats (example: Strength, Dexterity, Intelligence)
            int statSum = creature.Str + creature.Dex + creature.Int;

            // Base probability calculation
            double dropProbability = Math.Min(1.0, fame / 5000.0);

            // Ensure a minimum drop probability of 5% if statSum is 100 or more
            if (statSum >= 100)
            {
                dropProbability = Math.Max(dropProbability, 0.05);
            }

            if (_random.NextDouble() <= dropProbability)
            {
                // Determine the number of scrolls based on the stat sum
                int scrollCount;
                if (statSum < 1000)
                {
                    // Monsters with a stat sum less than 1000 drop exactly 1 scroll
                    scrollCount = 1;
                }
                else
                {
                    // Monsters with a stat sum of 1000 or higher drop between 2 and 3 scrolls
                    scrollCount = _random.Next(2, 4); // Randomly selects 2 or 3
                }

                // Add the scrolls to the corpse
                e.Corpse.AddItem(new AncientKnowledgeScroll(scrollCount));
            }

            //if (e.Creature.Str > 300) //Is it a strong creature? ¯\_(ツ)_/¯
            //{
            //	if (Server.RandomImpl.NextDouble() > 0.2) //~ 80% chance
            //	{
            //		//e.Corpse.AddItem(new BraceletOfHealth());
            //	}
            //}

            //if (e.Killer != null)
            //	if (e.Killer.LastKiller != e.Creature) //Did the player die to this creature? If not, lets give him a bonus
            //	{
            //		//e.Corpse.AddItem(new Gold(100));
            //	}
        }
    }
}
