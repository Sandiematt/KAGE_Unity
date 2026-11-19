namespace Kage
{
    /// <summary>The profile-driven half of the AI (SAN-95, ADR-0005).</summary>
    public partial class AIInputSource
    {
        /// <summary>The AI's profile (<see cref="CharacterDef.aiProfile"/>); null or <see cref="AiProfile.legacy"/> = the pre-SAN-95 behaviour.</summary>
        public AiProfile profile;

        bool Profiled => profile != null && !profile.legacy;

        /// <summary>Swaps the profile (a boss phase, ADR-0008). The parry rate follows the new profile.</summary>
        public void SetProfile(AiProfile p)
        {
            profile = p;
            parryRate = p != null ? p.parryRate : 0f;
        }
    }
}
