using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order Footman (id "order_footman", JS ENEMIES entry): the Order's rank-and-file cutter. JS stats from the generated
    /// tables (170 health, defence 0.054, damage 0.9, the cutter blade). He fights with Kage's katana move set at a move rate
    /// of 0.9, a little slower than Kage, with no move set of his own (ADR-0006). His look is <see cref="OrderFootmanPartSet"/>.
    /// </summary>
    public static class OrderFootman
    {
        public const string Id = "order_footman";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(OrderPalette.Eye, Color.white, OrderPalette.SilverDark);
            d.family = "katana";
            d.moveRate = 0.9f;
            return d;
        }
    }
}
