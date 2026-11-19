using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order Runner (id "order_runner", JS ENEMIES entry): a fast Order courier with a pair of shadow daggers. JS stats come
    /// from the generated tables (150 health, defence 0.03, damage 0.85, speed 1.12, the agile AI profile, build scale 0.97). He
    /// fights with the twin family's move set (<c>shadow_daggers</c>, JS base Tsubame) at full move rate, with no move set of his
    /// own (ADR-0006). His look is <see cref="OrderRunnerPartSet"/> in the shared <see cref="OrderPalette"/>.
    /// </summary>
    public static class OrderRunner
    {
        public const string Id = "order_runner";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(OrderPalette.Eye, Color.white, OrderPalette.SilverDark);
            d.family = "twin";
            return d;
        }
    }
}
