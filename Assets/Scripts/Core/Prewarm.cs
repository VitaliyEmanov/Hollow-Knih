using System;
using System.Collections.Generic;
using AshenWick.Art;
using AshenWick.World;

namespace AshenWick
{
    /// <summary>Queues all painting work for the background thread, in the order the game needs it.</summary>
    public static class Prewarm
    {
        public static void Start()
        {
            var jobs = new List<KeyValuePair<string, Func<ArtCanvas>>>();
            Action<string, Func<ArtCanvas>> add = (k, f) => jobs.Add(new KeyValuePair<string, Func<ArtCanvas>>(k, f));

            // splash + title first
            add("RaspberryLogo", ArtLibrary.RaspberryLogo);
            add("Divider", ArtLibrary.Divider);
            add("FlameIcon", ArtLibrary.FlameIcon);
            AddRoom(add, "outskirts");
            AddArea(add, 0);

            // Niv and common effects
            add("NivHead", ArtLibrary.NivHead); add("NivCloak", ArtLibrary.NivCloak); add("NivLeg", ArtLibrary.NivLeg);
            add("NivFlame", ArtLibrary.NivFlame); add("NivBlade", ArtLibrary.NivBlade); add("SlashArc", ArtLibrary.SlashArc);
            add("FlameIconEmpty", ArtLibrary.FlameIconEmpty); add("VesselFrame", ArtLibrary.VesselFrame);
            add("VesselFill", ArtLibrary.VesselFill); add("VesselBack", ArtLibrary.VesselBack); add("BossBarFrame", ArtLibrary.BossBarFrame);
            add("Candelabra", ArtLibrary.Candelabra); add("Tablet", ArtLibrary.Tablet); add("Candlemother", ArtLibrary.Candlemother);
            add("Chain", ArtLibrary.Chain); add("FirePillar", ArtLibrary.FirePillar); add("FlareBolt", ArtLibrary.FlareBolt);

            // first area and its creatures
            AddRoom(add, "fields");
            add("CrawlerBody", ArtLibrary.CrawlerBody); add("CrawlerLeg", ArtLibrary.CrawlerLeg);
            add("MothBody", ArtLibrary.MothBody); add("MothWing", ArtLibrary.MothWing);
            AddRoom(add, "scar");
            add("HuskBody", ArtLibrary.HuskBody); add("HuskLance", ArtLibrary.HuskLance);
            add("Altar", ArtLibrary.Altar); add("Relic", ArtLibrary.Relic); add("WaxShard", ArtLibrary.WaxShard);
            AddRoom(add, "nest");

            // Gornan
            AddArea(add, 1);
            AddRoom(add, "forge");
            add("GornanBody", ArtLibrary.GornanBody); add("GornanHead", ArtLibrary.GornanHead); add("GornanHammer", ArtLibrary.GornanHammer);
            add("GateBar", ArtLibrary.GateBar); add("Shockwave", ArtLibrary.Shockwave); add("Coal", ArtLibrary.Coal);
            add("FirePuddle", ArtLibrary.FirePuddle); add("Debris", ArtLibrary.Debris);

            // waxworks
            AddRoom(add, "waxworks");
            add("Chronicler", ArtLibrary.Chronicler); add("SpitterBody", ArtLibrary.SpitterBody); add("AshGlob", ArtLibrary.AshGlob);
            add("WispBody", ArtLibrary.WispBody);
            AddRoom(add, "shafts");

            // cathedral + Morra
            AddArea(add, 2);
            AddRoom(add, "nave");
            AddRoom(add, "weaver");
            add("WeaverBody", ArtLibrary.WeaverBody); add("WeaverWing", ArtLibrary.WeaverWing); add("WeaverMask", ArtLibrary.WeaverMask);
            add("Feather", ArtLibrary.Feather); add("TelegraphLine", ArtLibrary.TelegraphLine); add("DarknessHole", ArtLibrary.DarknessHole);

            // the hearth + the king
            AddArea(add, 3);
            AddRoom(add, "ascent");
            AddRoom(add, "heart");
            add("KingBody", ArtLibrary.KingBody); add("KingHead", ArtLibrary.KingHead); add("KingSword", ArtLibrary.KingSword);
            add("EmberOrb", ArtLibrary.EmberOrb); add("AshSpear", ArtLibrary.AshSpear); add("Beam", ArtLibrary.Beam);

            ArtCache.Prewarm(jobs);
        }

        static void AddRoom(Action<string, Func<ArtCanvas>> add, string id)
        {
            var def = RoomDefs.Get(id);
            if (def != null) add("Terrain_" + id, () => Room.TerrainFor(def));
        }

        static void AddArea(Action<string, Func<ArtCanvas>> add, int area)
        {
            add("Sky" + area, () => ArtLibrary.Sky(area));
            for (int d = 0; d < 3; d++)
            {
                int depth = d;
                add("Backdrop" + area + "_" + d, () => ArtLibrary.Backdrop(area, depth));
            }
        }
    }
}
