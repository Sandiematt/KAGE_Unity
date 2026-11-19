namespace Kage
{
    /// <summary>
    /// Something that reacts to what happened in a fight frame (ADR-0002 step 7, the late tick): a stretch's waves
    /// (<see cref="StretchSession"/>, SAN-89), see <c>FightSession</c> (SAN-102) for a duel's rounds, which its owner drives instead. Added to <c>ctx.LateTicks</c>; the fight
    /// loop calls <see cref="LateTick"/> after hits, projectiles and hazards. Gameplay reactions to events happen here, never inside
    /// an event callback (ADR-0003).
    /// </summary>
    public interface ILateTick
    {
        void LateTick();
    }
}
