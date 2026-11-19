namespace Kage
{
    /// <summary>
    /// Something that reacts to what happened in a fight frame (ADR-0002 step 7, the late tick): a stretch's waves
    /// (<see cref="StretchSession"/>, SAN-89), later boss phases and round logic (SAN-102). Added to <c>ctx.Sessions</c>; the fight
    /// loop calls <see cref="LateTick"/> after hits, projectiles and hazards. Gameplay reactions to events happen here, never inside
    /// an event callback (ADR-0003).
    /// </summary>
    public interface IFightSession
    {
        void LateTick();
    }
}
