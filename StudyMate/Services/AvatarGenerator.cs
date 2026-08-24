namespace StudyMate.Services
{
    /// <summary>
    /// Deterministic default avatar for a student with no uploaded photo — an initials
    /// monogram on a colored circle, the GitHub/Slack/Discord pattern, rather than a
    /// broken-image icon or a blank gray placeholder. Same student always produces the
    /// same avatar, and nothing here reads from user-controlled markup, so the SVG this
    /// returns is safe to render with Html.Raw.
    /// </summary>
    public static class AvatarGenerator
    {
        // Every entry sits in the warm family or the teal accent family, so a wall of
        // avatars reads as one palette. Cool purples and pinks were removed: they were
        // distinguishable but fought the terracotta theme everywhere they appeared.
        // All eight clear 4.5:1 against white text.
        private static readonly string[] Palette =
        {
            "#9A3412", // terracotta
            "#C2410C", // burnt orange
            "#B45309", // amber
            "#A16207", // dark gold
            "#047857", // teal
            "#0F766E", // deep teal
            "#9F1239", // deep rose (warm-leaning)
            "#4D7C0F"  // olive
        };

        public static string ColorFor(int studentId)
        {
            var index = (int)((uint)studentId % (uint)Palette.Length);
            return Palette[index];
        }

        public static string InitialsFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var initials = parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1],
                _ => $"{parts[0][0]}{parts[^1][0]}"
            };

            return initials.ToUpperInvariant();
        }

        /// <summary>Self-contained SVG markup — no external font dependency, sized by its viewBox.</summary>
        public static string SvgFor(int studentId, string name)
        {
            var color = ColorFor(studentId);
            var initials = System.Net.WebUtility.HtmlEncode(InitialsFor(name));

            return $"""
                <svg viewBox="0 0 48 48" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="{initials}">
                    <circle cx="24" cy="24" r="24" fill="{color}" />
                    <text x="24" y="24" text-anchor="middle" dominant-baseline="central"
                          font-family="'Plus Jakarta Sans', Arial, sans-serif" font-size="19"
                          font-weight="700" fill="#ffffff">{initials}</text>
                </svg>
                """;
        }
    }
}
