# Design guide

- Layout: single centered column, max-width 960px, generous spacing, responsive down to 360px; print-friendly (`@media print` keeps all content, white background).
- Palette (CSS variables): `--ink #14213d`, `--accent #e4572e` (own talk), `--ai #6c63ff`, `--dotnet #512bd4`, `--muted #6b7280`, `--card #ffffff`, `--bg #f5f7fb`.
- Hero: soft gradient from `--ink` to `--ai`, white text, round 112px avatar with a white ring. Initials avatar = circle with the initials in large bold letters.
- Timeline: vertical line on the left with time labels; each item is a card with a colored left border by track (AI/AI-ML = `--ai`, .NET = `--dotnet`, other = `--muted`). Breaks and the preparation slot are slim dashed cards.
- Own talk: larger card with `--accent` border, a "My talk" badge, and the room in bold.
- Track chips: small rounded pills.
- Typography: system font stack (`system-ui, -apple-system, Segoe UI, Roboto, sans-serif`); no web fonts.
- Accessibility: alt text on the photo, sufficient contrast, semantic headings (`h1` name, `h2` sections).
