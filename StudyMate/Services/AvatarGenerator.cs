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
        // A curated set that sits comfortably alongside Coral & Peach rather than
        // fighting it — no pure reds or greens that would misread as a status color.
        private static readonly string[] Palette =
        {
            "#F97316", "#0D9488", "#7C3AED", "#0891B2",
            "#CA8A04", "#DB2777", "#4338CA", "#059669"
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
