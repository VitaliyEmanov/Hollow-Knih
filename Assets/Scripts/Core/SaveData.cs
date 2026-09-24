using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Progress of Niv's pilgrimage. Stored as JSON in PlayerPrefs.</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "AshenWick.Save.v1";

        public string benchRoom = "outskirts";
        public int maxFlames = 5;
        public bool hasDash;
        public bool hasSpell;
        public bool hasDoubleJump;
        public List<string> shards = new List<string>();
        public List<string> bosses = new List<string>();
        public List<string> seenAreas = new List<string>();
        public List<string> talked = new List<string>();
        public bool finished;
        public float playTime;

        public bool BossDead(string id) { return bosses.Contains(id); }
        public bool HasShard(string id) { return shards.Contains(id); }

        public static bool Exists() { return PlayerPrefs.HasKey(Key); }

        public static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new SaveData();
            try
            {
                var s = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                return s ?? new SaveData();
            }
            catch (Exception)
            {
                return new SaveData();
            }
        }

        public void Write()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Erase()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
