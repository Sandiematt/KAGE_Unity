namespace Kage
{
    /// <summary>
    /// Builds the drawn parts of one character on the shared 20-bone gameplay skeleton (ADR-0005). The returned
    /// rig keeps Kage's pivots and parents; its parts, soft parts and colours are the part set's own.
    /// One file per part set in Character/PartSets, registered in <see cref="CharacterCatalog"/>.
    /// </summary>
    public interface IPartSet
    {
        string Id { get; }
        RigDef Build(CharacterDef c);
    }
}
