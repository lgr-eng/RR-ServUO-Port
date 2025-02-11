using System;
using Server;
using Server.Gumps;
using Server.Network;
using System.Collections;
using System.Collections.Generic;

namespace Server.Items
{
    public class UniversalPowerScroll : Item
    {
        [Constructable]
        public UniversalPowerScroll() : base(0x14F0) // Scroll item ID
        {
            Name = "Universal Power Scroll";
            LootType = LootType.Regular;
            Weight = 1.0;
            Hue = 1150; // Unique color
        }

        public UniversalPowerScroll(Serial serial) : base(serial)
        {
        }

        public override void AddNameProperties(ObjectPropertyList list)
        {
            base.AddNameProperties(list);
            list.Add(1070722, "Raises All Skills to 100");
            list.Add(1049644, "Universal Skill Power Scroll");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendMessage("This must be in your backpack to use it.");
                return;
            }

            foreach (Skill skill in from.Skills)
            {
                if (skill.Cap < 100.0)
                    skill.Cap = 100.0;
            }

            from.SendMessage("You feel a surge of knowledge as all your skills are enhanced!");

            Effects.SendLocationParticles(EffectItem.Create(from.Location, from.Map, EffectItem.DefaultDuration), 0, 0, 0, 0, 0, 5060, 0);
            Effects.PlaySound(from.Location, from.Map, 0x243);
            Effects.SendTargetParticles(from, 0x375A, 35, 90, 0x00, 0x00, 9502, (EffectLayer)255, 0x100);

            Delete();
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
