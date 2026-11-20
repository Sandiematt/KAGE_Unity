using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>Where the picture of a gear item comes from.</summary>
    public enum ArtSource
    {
        /// <summary>The weapon render of the Wonder design file (JS <c>WEAPON_ART</c>, src/img/ui_w_*.webp): the seven shop weapons.</summary>
        WeaponRender,
        /// <summary>The weapon the JS draws itself on a canvas (<c>drawWeaponIcon</c>): every other weapon.</summary>
        WeaponIcon,
        /// <summary>The helmet the JS draws into its tile (<c>paintIcon</c>: a head with the helmet on).</summary>
        HelmetIcon,
        /// <summary>The armour the JS draws into its card and results chip: Kage wearing it.</summary>
        ArmorIcon,
    }

    /// <summary>The picture a tile, chip or card shows for an item: where it comes from and the image's name under Resources/UI/Img.</summary>
    public sealed class TileArt
    {
        public ArtSource Source;
        public string Key;

        public Texture2D Texture { get { return Ui.Tex(Key); } }
    }

    /// <summary>
    /// Which drawn picture shows each item of the Armory (SAN-67 rework): the sword, the helmet or the armour itself, the way the JS draws it.
    /// The seven shop weapons have the design file's renders; every other weapon, every helmet and every armour is the JS canvas drawing, exported
    /// to PNG by <c>Tools/js_ui_capture/export_art.mjs</c>. The Weapons tab's big preview is the JS illustration of the blade (<see cref="PreviewKey"/>).
    /// </summary>
    public static class GearArt
    {
        // JS WEAPON_ART: the weapons that have a render
        static readonly HashSet<string> Renders = new HashSet<string> { "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki" };

        public static bool HasRender(string weaponId) { return weaponId != null && Renders.Contains(weaponId); }

        public static TileArt For(GearKind kind, string id)
        {
            switch (kind)
            {
                case GearKind.Weapons:
                    return HasRender(id)
                        ? new TileArt { Source = ArtSource.WeaponRender, Key = "ui_w_" + id }
                        : new TileArt { Source = ArtSource.WeaponIcon, Key = "icon_w_" + id };
                case GearKind.Helmets:
                    return new TileArt { Source = ArtSource.HelmetIcon, Key = "icon_h_" + id };
                default:
                    return new TileArt { Source = ArtSource.ArmorIcon, Key = "icon_a_" + id };
            }
        }

        /// <summary>The JS illustration of a weapon for the Armory's preview (unrotated, its origin at 350/600 across and half way down a 1200 x 600 image).</summary>
        public static string PreviewKey(string weaponId) { return "big_w_" + weaponId; }

        /// <summary>The faint warrior behind the weapon on the preview.</summary>
        public const string GhostKey = "armory_ghost";
    }
}
