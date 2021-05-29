using System.Collections.Generic;

namespace HealthBars
{
    public class HealthBarsConfig
    {
        /// <summary>If the actual health bar bar (inner part) is pixel aligned with the texture</summary>
        public bool HealthBarIsPixelAligned = true;

        /// <summary>The offset of the health bar above the top sprite edge in **texture pixels**</summary>
        public int HealthBarOffset = 0;

        /// <summary>
        ///     The individual monster type health bar offset, positive values
        ///     indicate the health bar is further down
        /// </summary>
        public Dictionary<string, int> MonsterTypeOffset = new Dictionary<string, int>
        {
            {"AngryRoger", -5},
            {"Bat", 9},
            {"BigSlime", 1},
            {"BlueSquid", 5},
            {"Bug", -10},
            {"DinoMonster", 1},
            {"DustSpirit", 12},
            {"DwarvishSentry", -6},
            {"Fly", -3},
            {"Ghost", -5},
            {"GreenSlime", 4},
            {"Grub", 6},
            {"HotHead", 0},
            {"LavaCrab", 2},
            {"LavaLurk", 0},
            {"Leaper", 6},
            {"MetalHead", 0},
            {"Mummy", -3},
            {"RockCrab", 2},
            {"RockGolem", -5},
            {"Serpent", 12},
            {"ShadowBrute", -4},
            {"ShadowShaman", -6},
            {"Shooter", -4},
            {"Skeleton", -5},
            {"Spiker", 4},
            {"SquidKid", -5}
        };

        /// <summary>If the health bar should include the current monster health as text</summary>
        public bool ShowHealthNumbers = false;
    }
}