using System;
using Server.Items;

namespace Server.Items
{
    public class AncientKnowledgeScroll : Item
    {
        [Constructable]
        public AncientKnowledgeScroll() : this(1)
        {
        }

        [Constructable]
        public AncientKnowledgeScroll(int amount) : base(0xE34)  //0xE34 is the itemID for a scroll. You can change this to another itemID if desired.
        {
            Stackable = true;
            Amount = amount;
            Weight = 0.01;
            Name = "Ancient Knowledge Scroll";
            Hue = 1150;  // Color of the scroll, change as required.
        }

        public AncientKnowledgeScroll(Serial serial) : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }
    }
}
