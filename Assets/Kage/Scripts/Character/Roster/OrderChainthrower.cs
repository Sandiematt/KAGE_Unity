using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order Chainthrower (id "order_chainthrower", JS ENEMIES entry): a chain-sickle soldier who keeps mid range and lashes.
    /// JS stats come from the generated tables (150 health, defence 0.04, damage 0.85, the ranged AI profile, build scale 0.98).
    /// He fights with the chain family's move set (<c>chain_hook</c>, JS base Yamigama) at full move rate, with no move set of his
    /// own (ADR-0006). His look is <see cref="OrderChainthrowerPartSet"/> in the shared <see cref="OrderPalette"/>.
    /// </summary>
    public static class OrderChainthrower
    {
        public const string Id = "order_chainthrower";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(OrderPalette.Eye, Color.white, OrderPalette.SilverDark);
            d.family = "chain";
            return d;
        }
    }
}
