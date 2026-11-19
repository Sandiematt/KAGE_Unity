using System;
using System.Collections.Generic;
using System.Linq;
using Kage.Rules;

namespace Kage
{
    /// <summary>The three tabs of the Armory (JS armTab: weapons, helmets, armors).</summary>
    public enum ArmoryTab
    {
        Weapons,
        Helmets,
        Armor,
    }

    /// <summary>The two rows of the Armor tab: the eight-armour line (Shinobi Garb to Mugen) and the trophies.</summary>
    public enum ArmorLine
    {
        Line,
        Trophy,
    }

    /// <summary>What the main button of the detail panel does.</summary>
    public enum ArmoryAction
    {
        None,
        Buy,
        Upgrade,
    }

    /// <summary>One item in the row of tiles (or cards) at the bottom of the screen.</summary>
    public sealed class ArmoryTile
    {
        public string Id, Name;
        public bool Owned, Equipped, Locked, Selected;
        public int Level;
        /// <summary>What it costs (the shop price in coins and in gems); 0 for what the item is not paid in.</summary>
        public int Coins, Gems;
        /// <summary>The tier label shown on a locked tile instead of a price ("Boss weapon", "Trophy").</summary>
        public string LockLabel;
    }

    /// <summary>One bar of the stat list: the value now, what the next level adds, and both as a share of the bar.</summary>
    public sealed class ArmoryStat
    {
        public string Label, Value, Preview;
        /// <summary>The filled part and the preview part of the bar, 0 to 100.</summary>
        public double Pct, PreviewPct;
    }

    /// <summary>The gold box (perks, rage art) or the red box (locked) under the stats.</summary>
    public sealed class ArmoryBox
    {
        public string Title, Text;
        public bool Warn;
    }

    /// <summary>A button of the detail panel. For Buy and Upgrade, <see cref="Coins"/> and <see cref="Gems"/> are the price.</summary>
    public sealed class ArmoryButton
    {
        public string Label;
        public ArmoryAction Action;
        public int Coins, Gems;
        public bool Enabled;
    }

    /// <summary>
    /// What the Armory shows for a save, a tab and a selection (JS renderArmory), as plain data: the tiles, the selected item's
    /// tier, level, stats with the next-level preview, perks or rage art, the lock hint and what the Buy / Upgrade and Equip
    /// buttons say and whether they work. Every number comes from the rules (<see cref="GameRules"/>); the screen only draws this.
    /// </summary>
    public sealed class ArmoryView
    {
        public ArmoryTab Tab;
        public GearKind Kind;
        public ArmoryTile[] Tiles;
        public string SelectedId;
        /// <summary>The row showing on the Armor tab.</summary>
        public ArmorLine Line;
        public int TrophyCount;

        public string TierId, TierLine, Name, Lore, Desc;
        /// <summary>The big watermark kanji at the top left (the weapon's name in kanji, 兜 for helmets, 鎧 for armour).</summary>
        public string Kanji;
        public bool Owned, Equipped, Locked;
        /// <summary>The owned level, 0 when not owned.</summary>
        public int Level, MaxLevel;
        public string LevelText;
        public ArmoryStat[] Stats;
        public ArmoryBox[] Boxes;
        public ArmoryButton Primary, Equip;

        public static GearKind KindOf(ArmoryTab tab)
        {
            return tab == ArmoryTab.Weapons ? GearKind.Weapons : tab == ArmoryTab.Helmets ? GearKind.Helmets : GearKind.Armors;
        }

        static int Round(double x) { return (int)Math.Floor(x + 0.5); }

        static string EquippedId(SaveData save, GearKind kind)
        {
            return kind == GearKind.Weapons ? save.equipped.weapon : kind == GearKind.Helmets ? save.equipped.helmet : save.equipped.armor;
        }

        static int Cost(GearItem it) { return it.Price + it.Gems * 100; }

        /// <summary>The row that shows when none was chosen: the trophies when a trophy armour is worn.</summary>
        public static ArmorLine DefaultLine(SaveData save)
        {
            var worn = JsTables.Armor(save.equipped.armor);
            return worn != null && worn.Tier == "trophy" ? ArmorLine.Trophy : ArmorLine.Line;
        }

        /// <summary>
        /// The ids of a tab in the order the Armory lists them: the shop from the cheapest tier to the dearest and, inside a tier,
        /// by price (a gem counts as 100 coins), then the trophies and boss weapons. Enemy-only gear and bare hands are left out.
        /// The Armor tab lists one row only.
        /// </summary>
        public static string[] Items(ArmoryTab tab, ArmorLine line)
        {
            var kind = KindOf(tab);
            IEnumerable<GearItem> items;
            if (kind == GearKind.Weapons) items = JsTables.Weapons.Select(w => GameRules.Item(kind, w.Id));
            else if (kind == GearKind.Helmets) items = JsTables.Helmets.Select(g => GameRules.Item(kind, g.Id));
            else items = JsTables.Armors.Select(g => GameRules.Item(kind, g.Id));
            items = items.Where(i => !i.Enemy && i.Id != "fists");
            if (tab == ArmoryTab.Armor)
            {
                bool trophies = line == ArmorLine.Trophy;
                items = items.Where(i => (i.Tier == "trophy") == trophies);
            }
            return items
                .OrderBy(i => JsTables.Tier(string.IsNullOrEmpty(i.Tier) ? "common" : i.Tier).Rank)
                .ThenBy(Cost)
                .Select(i => i.Id)
                .ToArray();
        }

        /// <summary>The save of the preview Kage: the equipped gear with the selection swapped in. The real save is not touched.</summary>
        public static SaveData PreviewSave(SaveData save, ArmoryTab tab, string id)
        {
            var e = save.equipped;
            var preview = new SaveData
            {
                level = save.level,
                owned = save.owned,
                equipped = new Equipped { weapon = e.weapon, helmet = e.helmet, armor = e.armor },
            };
            switch (tab)
            {
                case ArmoryTab.Weapons: preview.equipped.weapon = id; break;
                case ArmoryTab.Helmets: preview.equipped.helmet = id; break;
                default:
                    // the armour cards wear the Crimson Scarf, so every card shows the armour alone (JS startArmorCards)
                    preview.equipped.armor = id;
                    preview.equipped.helmet = "shiro_maki";
                    break;
            }
            return preview;
        }

        public static ArmoryView Build(SaveData save, ArmoryTab tab, string selectedId = null, ArmorLine? armorLine = null)
        {
            var kind = KindOf(tab);
            var line = armorLine ?? DefaultLine(save);
            var ids = Items(tab, line);
            string worn = EquippedId(save, kind);
            string id = selectedId;
            if (id == null || !ids.Contains(id)) id = worn;
            if (!ids.Contains(id) && ids.Length > 0) id = ids[0];

            var item = GameRules.Item(kind, id);
            int owned;
            GameRules.OwnedOf(save, kind).TryGetValue(id, out owned);
            if (owned < 0) owned = 0;
            int lv1 = Math.Max(1, owned);
            int max = GameRules.MaxLevel(kind, id);
            bool equipped = worn == id;
            bool canUp = owned > 0 && owned < max;
            int next = Math.Min(max, lv1 + 1);

            var v = new ArmoryView
            {
                Tab = tab,
                Kind = kind,
                SelectedId = id,
                Line = line,
                TrophyCount = tab == ArmoryTab.Armor ? Items(ArmoryTab.Armor, ArmorLine.Trophy).Length : 0,
                TierId = GameRules.ItemTier(kind, id, lv1),
                Name = item.Name,
                Owned = owned > 0,
                Equipped = equipped,
                Level = owned,
                MaxLevel = max,
                LevelText = owned > 0 ? owned + " / " + max : "Not owned",
                Locked = GameRules.IsLocked(save, kind, id),
                Kanji = ArmoryText.Kanji(tab, id),
            };
            string tierLabel = GameRules.TierLabel(kind, id, lv1);
            if (kind == GearKind.Weapons)
            {
                v.TierLine = tierLabel + (string.IsNullOrEmpty(item.Weapon.Cls) ? "" : " · " + item.Weapon.Cls);
                v.Lore = item.Weapon.Lore;
                v.Desc = item.Weapon.Desc;
            }
            else
            {
                v.TierLine = tierLabel;
                v.Lore = item.Gear.Lore;
            }

            var stats = new List<ArmoryStat>();
            var boxes = new List<ArmoryBox>();
            if (kind == GearKind.Weapons)
            {
                var s = GameRules.WeaponStats(id, lv1);
                var n = GameRules.WeaponStats(id, next);
                stats.Add(WeaponStat("Damage", s.Damage, canUp ? n.Damage - s.Damage : 0));
                stats.Add(WeaponStat("Speed", s.Speed, canUp ? n.Speed - s.Speed : 0));
                stats.Add(WeaponStat("Reach", s.Reach, canUp ? n.Reach - s.Reach : 0));
                string rageName, rageDesc;
                if (ArmoryText.RageArt(id, out rageName, out rageDesc))
                    boxes.Add(new ArmoryBox { Title = "RAGE ART · " + rageName, Text = rageDesc });
            }
            else
            {
                int dNow = Round(GameRules.GearDef(kind, id, lv1) * 100), dNext = Round(GameRules.GearDef(kind, id, next) * 100);
                int hNow = GameRules.GearHp(kind, id, lv1), hNext = GameRules.GearHp(kind, id, next);
                var perks = item.Gear.Perks ?? new Dictionary<string, double>();
                double speedPerk;
                perks.TryGetValue("speed", out speedPerk);
                int mobility = Round(100 + speedPerk * 100 * 3);
                int dUp = canUp ? dNext - dNow : 0, hUp = canUp && hNext > hNow ? hNext - hNow : 0;
                stats.Add(new ArmoryStat { Label = "Defense", Value = dNow + "%", Pct = Clamp(dNow * 3.5), Preview = Plus(dUp), PreviewPct = PreviewShare(dNow * 3.5, (dNext - dNow) * 3.5, dUp) });
                stats.Add(new ArmoryStat { Label = "Health", Value = "+" + hNow, Pct = Clamp(hNow), Preview = Plus(hUp), PreviewPct = PreviewShare(hNow, hNext - hNow, hUp) });
                stats.Add(new ArmoryStat { Label = "Mobility", Value = mobility.ToString(), Pct = Clamp(mobility - 30), Preview = "", PreviewPct = 0 });
                string perkText = ArmoryText.Perks(perks);
                if (perkText.Length > 0) boxes.Add(new ArmoryBox { Title = "PERKS", Text = perkText });
            }
            if (v.Locked) boxes.Add(new ArmoryBox { Title = "LOCKED", Text = GameRules.LockHint(kind, id), Warn = true });
            // the Armor tab always has a box under its stats
            if (tab == ArmoryTab.Armor && boxes.Count == 0) boxes.Add(new ArmoryBox { Title = "PERKS", Text = "None: the plain coat he left home in" });
            v.Stats = stats.ToArray();
            v.Boxes = boxes.ToArray();

            v.Primary = PrimaryButton(save, kind, item, owned, max);
            v.Equip = equipped
                ? new ArmoryButton { Label = "Equipped", Enabled = false }
                : new ArmoryButton { Label = "Equip", Enabled = owned > 0 };

            v.Tiles = ids.Select(tid =>
            {
                var it = GameRules.Item(kind, tid);
                int lvl;
                GameRules.OwnedOf(save, kind).TryGetValue(tid, out lvl);
                return new ArmoryTile
                {
                    Id = tid,
                    Name = it.Name,
                    Level = lvl,
                    Owned = lvl > 0,
                    Equipped = EquippedId(save, kind) == tid,
                    Locked = it.Exclusive && lvl <= 0,
                    Selected = tid == id,
                    Coins = it.Price,
                    Gems = it.Gems,
                    LockLabel = it.Exclusive ? GameRules.TierLabel(kind, tid, 1) : null,
                };
            }).ToArray();
            return v;
        }

        static ArmoryButton PrimaryButton(SaveData save, GearKind kind, GearItem item, int owned, int max)
        {
            if (owned <= 0)
            {
                if (item.Exclusive) return new ArmoryButton { Label = "Locked", Enabled = false };
                return new ArmoryButton
                {
                    Label = "Buy",
                    Action = ArmoryAction.Buy,
                    Coins = item.Price,
                    Gems = item.Gems,
                    Enabled = save.coins >= item.Price && save.gems >= item.Gems,
                };
            }
            if (owned >= max) return new ArmoryButton { Label = "Mastered", Enabled = false };
            int cost = GameRules.UpgradeCost(kind, item.Id, owned);
            return new ArmoryButton { Label = "Upgrade", Action = ArmoryAction.Upgrade, Coins = cost, Enabled = save.coins >= cost };
        }

        static ArmoryStat WeaponStat(string label, int now, int up)
        {
            return new ArmoryStat { Label = label, Value = now.ToString(), Pct = Clamp(now / 1.8), Preview = Plus(up), PreviewPct = PreviewShare(now / 1.8, up / 1.8, up) };
        }

        static string Plus(int n) { return n != 0 ? "+" + n : ""; }

        static double Clamp(double pct) { return Math.Max(0, Math.Min(100, pct)); }

        // the preview part sits after the filled part and stops at the end of the bar (JS: clamp(upPct, 0, 100 - pct))
        static double PreviewShare(double pct, double upPct, int up)
        {
            if (up == 0) return 0;
            return Math.Max(0, Math.Min(100 - Clamp(pct), upPct));
        }
    }

    /// <summary>The Armory's words that are not in the generated tables: perk wording, kanji and the rage-art names (JS UIManager, SPECIALS).</summary>
    public static class ArmoryText
    {
        static readonly Dictionary<string, string> WeaponKanji = new Dictionary<string, string>
        {
            { "kagetsu", "影月" }, { "tsubame", "燕" }, { "shizuka", "静" }, { "onikiri", "鬼斬" }, { "mizuchi", "蛟" },
            { "yamigama", "闇鎌" }, { "iwakudaki", "岩砕き" }, { "shinsen", "神閃" },
        };

        // JS SPECIALS by weapon id (WEAPONS[id].special): the line under a weapon in the Armory
        static readonly Dictionary<string, string[]> Rage = new Dictionary<string, string[]>
        {
            { "fists", new[] { "Shadow Palm", "A burst of ink that hurls the enemy away." } },
            { "kagetsu", new[] { "Crimson Moon", "A blood moon rises. Three crimson crescent cuts from both sides, then the blade is sheathed and the giant crescent falls through the enemy. Unblockable." } },
            { "tsubame", new[] { "Thousand Swallows", "Strike from eight directions in a blink, leaving lightning afterimages that all strike at once in a lightning X. Unblockable." } },
            { "shizuka", new[] { "Silent Moon", "The world goes silent. One unseen draw, a lattice of violet cuts, and they all open as the blade clicks home. Unblockable." } },
            { "onikiri", new[] { "Hell Splitter", "A demon leap and an overhead that splits the ground: fissures race both ways and lava pillars erupt along them. Unblockable." } },
            { "mizuchi", new[] { "Rising Water Dragon", "The arena floods. A giant water dragon bursts from the spear, coils round the enemy and crashes down on them. Unblockable." } },
            { "yamigama", new[] { "Reaper's Requiem", "Darkness falls and three shadow clones rise: they chain the enemy and cut in turn, then two enormous scythe crescents carve down. Unblockable." } },
            { "iwakudaki", new[] { "Golden Earthbreaker", "Rocks rise and the ground trembles; one slam sends three golden shockwaves racing out. Every grounded enemy is stunned. Unblockable." } },
            { "shinsen", new[] { "Final Flow", "Rising, overhead and diagonal cuts from both sides, then one decisive horizontal cut. Unblockable." } },
            { "raiko_no_tachi", new[] { "Thunder Swing", "A wide arc that sends red lightning racing along the ground. Unblockable." } },
            { "kagehira_yurei", new[] { "Smoke Step", "Vanish in a puff of smoke, leave an afterimage behind, and strike from behind the enemy. Unblockable." } },
            { "mugen_no_ha", new[] { "Echo Replication", "A phantom memory-shard copy of Kage replays his most recent landed string on the enemy, harder. Unblockable." } },
        };

        public static string Kanji(ArmoryTab tab, string id)
        {
            if (tab == ArmoryTab.Helmets) return "兜";
            if (tab == ArmoryTab.Armor) return "鎧";
            string k;
            return WeaponKanji.TryGetValue(id ?? "", out k) ? k : "武器";
        }

        /// <summary>The kanji after a weapon's name, or null when it has none.</summary>
        public static string NameKanji(string weaponId)
        {
            string k;
            return WeaponKanji.TryGetValue(weaponId ?? "", out k) ? k : null;
        }

        public static bool RageArt(string weaponId, out string name, out string desc)
        {
            string[] r;
            if (Rage.TryGetValue(weaponId ?? "", out r)) { name = r[0]; desc = r[1]; return true; }
            name = desc = null;
            return false;
        }

        static int Pct(double v) { return (int)Math.Floor(v * 100 + 0.5); }

        /// <summary>The perks in JS wording, joined with " · " (empty when there are none).</summary>
        public static string Perks(Dictionary<string, double> perks)
        {
            var parts = new List<string>();
            foreach (var kv in perks)
            {
                double v = kv.Value;
                switch (kv.Key)
                {
                    case "blockRage": parts.Add("blocking builds double rage"); break;
                    case "speed": parts.Add((v > 0 ? "+" : "") + Pct(v) + "% speed"); break;
                    case "rage": parts.Add("+" + Pct(v) + "% rage gain"); break;
                    case "stunRes": parts.Add(Pct(v) + "% stun resist"); break;
                    case "dash": parts.Add("+" + Pct(v) + "% dash"); break;
                    case "ammo": parts.Add("+" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " kunai"); break;
                    case "kunai": parts.Add("+" + Pct(v) + "% kunai damage"); break;
                    case "dmg": parts.Add("+" + Pct(v) + "% damage"); break;
                }
            }
            return string.Join(" · ", parts);
        }
    }
}
