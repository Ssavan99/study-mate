namespace StudyMate.Models
{
    /// <summary>
    /// The only thing about a match score that is ever shown to a student. The raw
    /// number still drives ranking internally, but the interface only ever displays
    /// a tier, and only for the top two — everything else renders as a plain card
    /// with no badge, so nobody is ever labeled with a negative or middling category.
    /// </summary>
    public enum MatchTier
    {
        None = 0,
        Strong = 1,
        Great = 2
    }

    public static class MatchTierExtensions
    {
        public const int GreatThreshold = 75;
        public const int StrongThreshold = 55;

        public static MatchTier ToTier(this int score) => score switch
        {
            >= GreatThreshold => MatchTier.Great,
            >= StrongThreshold => MatchTier.Strong,
            _ => MatchTier.None
        };
    }
}
