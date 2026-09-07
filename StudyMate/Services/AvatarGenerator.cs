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
        // The six pastel color-block tokens from the design system. Ordered to alternate
        // cool and warm so consecutive student ids stay easy to tell apart in a list.
        // These are grounds, not ink: initials are drawn in black, and every entry clears
        // 10:1 against it (lilac 10.9, coral 13.8, pink 15.0, mint 15.7, lime 16.9,
        // cream 17.8). White initials would fail on all six, which is why the text fill
        // below is ink rather than the white it used to be.
        private static readonly string[] Palette =
        {
            "#C5B0F4", // lilac
            "#DCEEB1", // lime
            "#F3C9B6", // coral
            "#C8E6CD", // mint
            "#EFD4D4", // pink
            "#F4ECD6"  // cream
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
                          font-family="Inter, -apple-system, Arial, sans-serif" font-size="19"
                          font-weight="700" fill="#000000">{initials}</text>
                </svg>
                """;
        }
    }
}
