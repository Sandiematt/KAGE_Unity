namespace Kage
{
    /// <summary>A test dummy (id "test_heavy", dev only): the Ronin with three times Kage's mass (SAN-68).</summary>
    public static class TestHeavy
    {
        public const string Id = "test_heavy";

        public static CharacterDef Def()
        {
            var d = Ronin.Def();
            d.id = Id;
            d.displayName = "HEAVY";
            d.mass = 3f;
            return d;
        }
    }
}
