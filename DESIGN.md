---
name: Eascess
description: Enterprise web accessibility as a managed service — WCAG 2.2 AAA, one script tag.
colors:
  instrument-teal: "oklch(72% 0.11 190)"
  instrument-teal-deep: "oklch(62% 0.12 200)"
  dusk-navy: "oklch(42% 0.15 270)"
  dusk-navy-deep: "oklch(32% 0.14 270)"
  warm-cream: "oklch(98% 0.006 85)"
  warm-cream-100: "oklch(96% 0.008 85)"
  warm-cream-200: "oklch(93% 0.01 85)"
  warm-cream-300: "oklch(88% 0.012 80)"
  ink-900: "oklch(18% 0.012 260)"
  ink-800: "oklch(24% 0.012 260)"
  ink-700: "oklch(36% 0.014 260)"
  ink-600: "oklch(48% 0.014 260)"
  ink-500: "oklch(60% 0.012 260)"
  success: "oklch(62% 0.14 155)"
  warning: "oklch(72% 0.15 75)"
  danger: "oklch(60% 0.18 27)"
typography:
  display:
    fontFamily: "'Instrument Serif', 'Times New Roman', Georgia, serif"
    fontSize: "clamp(3.25rem, 6vw, 5.5rem)"
    fontWeight: 400
    lineHeight: 0.95
    letterSpacing: "-0.035em"
  headline:
    fontFamily: "'Instrument Serif', 'Times New Roman', Georgia, serif"
    fontSize: "clamp(1.75rem, 4vw, 2.75rem)"
    fontWeight: 400
    lineHeight: 1
    letterSpacing: "-0.02em"
  title:
    fontFamily: "'Inter', -apple-system, sans-serif"
    fontSize: "15px"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  body:
    fontFamily: "'Inter', -apple-system, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.55
  label:
    fontFamily: "'Inter', -apple-system, sans-serif"
    fontSize: "12px"
    fontWeight: 600
    letterSpacing: "0.08em"
rounded:
  xs: "6px"
  sm: "10px"
  md: "14px"
  lg: "20px"
  xl: "28px"
components:
  button-brand:
    backgroundColor: "{colors.instrument-teal}"
    textColor: "#ffffff"
    rounded: "{rounded.sm}"
    padding: "11px 18px"
  button-brand-hover:
    backgroundColor: "{colors.instrument-teal-deep}"
    textColor: "#ffffff"
    rounded: "{rounded.sm}"
    padding: "11px 18px"
  button-primary:
    backgroundColor: "{colors.ink-900}"
    textColor: "{colors.warm-cream}"
    rounded: "{rounded.sm}"
    padding: "11px 18px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.ink-700}"
    rounded: "{rounded.sm}"
    padding: "11px 18px"
  card:
    backgroundColor: "#ffffff"
    textColor: "{colors.ink-900}"
    rounded: "{rounded.md}"
    padding: "20px"
  input:
    backgroundColor: "#ffffff"
    textColor: "{colors.ink-900}"
    rounded: "{rounded.sm}"
    padding: "12px 14px"
---

# Design System: Eascess

## 1. Overview

**Creative North Star: "The Quiet Audit"**

Eascess is a product that sells trust. Its customers are enterprise IT managers and compliance officers who must be convinced that a third-party script is safe enough to inject into a production website. The visual language earns that trust the same way a well-prepared audit report does: precision, consistency, and calm. Nothing is decorative. Nothing performs. The Warm Cream surfaces give the system human warmth; the Instrument Serif display type carries authority; the Inter body copy stays highly legible under any ambient light. The result should feel like it was designed by engineers who also understand craft, not by a marketing team trying to look technical.

The system uses a restrained color strategy. The tinted cream surface dominates approximately 90% of every screen. Instrument Teal and Dusk Navy appear only at the edges: the primary CTA, the brand mark, active navigation states, and semantic status signals. Their scarcity is the point. When the gradient fires, it means something.

This system explicitly rejects: consumer-startup playfulness (neon, rainbow gradients, cartoon iconography), cold government flatness (clinical white, grey-on-grey data tables, link-blue typography), and dark-mode-first aesthetics. It also rejects the SaaS dashboard reflex of identical metric-card grids and big-number hero sections.

**Key Characteristics:**
- Warm cream surfaces (not white, not grey) as the universal ground
- Instrument Serif at display and headline scale only; Inter everywhere else
- A single gradient accent (Instrument Teal to Dusk Navy) reserved for the brand mark and primary CTA
- Shadows appear only on state change, never as ambient decoration
- Animations under 200ms, exponential ease-out only; always suppressed under `prefers-reduced-motion`
- WCAG 2.2 AAA contrast as a non-negotiable baseline, not a fallback pass condition

## 2. Colors: The Warm Cream Palette

Two accent tones, one gradient, a warm cream neutral scale, and a cool-cast ink scale. Nothing more.

### Primary

- **Instrument Teal** (`oklch(72% 0.11 190)` / deep: `oklch(62% 0.12 200)`): The lighter face of the brand gradient. Used as the leading stop in the primary CTA gradient, the focus ring, the form field focus glow, and active badge accents. Never used as a flat fill on large surface areas.

### Secondary

- **Dusk Navy** (`oklch(42% 0.15 270)` / deep: `oklch(32% 0.14 270)`): The closing stop of the brand gradient. Also the primary link accent within authenticated app surfaces. Carries weight and gravity; used sparingly to anchor structure.

### Neutral: Warm Cream

- **Warm Cream / Page Ground** (`oklch(98% 0.006 85)`): The page background. Always carries a faint amber tint (chroma 0.006, hue 85). Never pure white.
- **Warm Cream 100** (`oklch(96% 0.008 85)`): Sidebar backgrounds, table header fills, secondary surface layers.
- **Warm Cream 200** (`oklch(93% 0.01 85)`): Hover backgrounds on interactive rows, inline code backgrounds, muted badge fill.
- **Warm Cream 300** (`oklch(88% 0.012 80)`): Borders, dividers, hairlines. The single stroke that separates surfaces without creating visual weight.

### Neutral: Ink

- **Ink 900** (`oklch(18% 0.012 260)`): Primary text, active navigation fill. The darkest point in the system.
- **Ink 800** (`oklch(24% 0.012 260)`): Card titles, form labels, data cells.
- **Ink 700** (`oklch(36% 0.014 260)`): Secondary body text, nav link text, descriptive prose.
- **Ink 600** (`oklch(48% 0.014 260)`): Supporting text, timestamps, caption-level content.
- **Ink 500** (`oklch(60% 0.012 260)`): Meta labels, navigation section headers, empty-state supporting text.

The Ink scale carries a deliberate blue cast (hue 260). It reads as near-black against warm cream surfaces without the lifelessness of a pure grey.

### Semantic

- **Success** (`oklch(62% 0.14 155)`): WCAG compliance confirmations, positive score indicators.
- **Warning** (`oklch(72% 0.15 75)`): Partial compliance, pending scan states.
- **Danger** (`oklch(60% 0.18 27)`): Critical failures, destructive actions.

### Named Rules

**The One Voice Rule.** The brand gradient (Instrument Teal to Dusk Navy at 135 degrees) fires exactly twice per screen: the primary CTA button and the brand mark. It does not appear as a decorative background wash, a card accent, or a text fill. Its rarity is what makes it signal authority instead of noise.

**The Parchment Test.** If any surface reads as stark white at arm's length, it is wrong. Every background must use the Warm Cream scale. Pure white is reserved only for card surfaces that sit visibly raised atop the cream page ground. The contrast between card white and cream page is the layering mechanism.

## 3. Typography

**Display Font:** Instrument Serif (with Times New Roman, Georgia, serif fallback)
**Body Font:** Inter (with -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif fallback)
**Mono Font:** JetBrains Mono (with ui-monospace, SFMono-Regular, Menlo fallback)

**Character:** Instrument Serif at large scale brings editorial authority to a product that handles technical compliance work. At display size its tight leading (0.95) and negative tracking (-0.035em) feel architectural rather than decorative. Paired with Inter's high legibility at small sizes, the combination communicates both craft and function without strain.

### Hierarchy

- **Display** (400 weight, `clamp(3.25rem, 6vw, 5.5rem)`, line-height 0.95, letter-spacing -0.035em): Hero headlines and singular page-level statements. One per screen maximum. Instrument Serif only.
- **Headline** (400 weight, `clamp(1.75rem, 4vw, 2.75rem)`, line-height 1, letter-spacing -0.02em): Section headings, feature titles, marketing sub-headlines. Instrument Serif only.
- **Title** (600 weight, `15px`, line-height 1.3, letter-spacing -0.01em): Card headers, sidebar section labels, table section titles. Inter only.
- **Body** (400 weight, `16px`, line-height 1.55): All descriptive prose. Maximum line length 65ch on marketing surfaces, 72ch in documentation. Inter only.
- **Label** (600 weight, `12px`, letter-spacing 0.08em, uppercase): Navigation section dividers, table column headers, eyebrow tags, badge text. Inter only. Uppercase is permitted only at this scale; never apply uppercase to body or title copy.

### Named Rules

**The Quiet Serif Rule.** Instrument Serif appears only at Display and Headline scale. It is never set at body or label size. A small-size Instrument Serif is a misconfigured template, not a design choice; the font earns its authority through scale and restraint.

**The Italic Authority Rule.** Italic Instrument Serif appears only for emphasis within a Display or Headline span, never as a stylistic default. It carries specific semantic weight (a brand promise, a key phrase) and should occur at most once per headline.

## 4. Elevation

This system is flat by default. Surfaces do not carry ambient shadows at rest. Elevation is a response to state change, not a decorative layer.

The sole structural exception is the card. A card carries `sh-sm` as the minimum lift necessary to confirm its boundary above the cream page ground. This is not decorative; it is the layering mechanism that makes the cream-and-white surface hierarchy legible.

### Shadow Vocabulary

- **sh-sm** (`0 1px 2px rgba(16,24,40,.06), 0 1px 3px rgba(16,24,40,.04)`): Default card lift. Barely perceptible; structural. The baseline elevation.
- **sh-md** (`0 4px 12px rgba(16,24,40,.06), 0 2px 4px rgba(16,24,40,.04)`): Hover state for interactive cards, focused panels. Signals responsiveness.
- **sh-lg** (`0 16px 40px rgba(16,24,40,.1), 0 4px 12px rgba(16,24,40,.05)`): Marketing illustrations and decorative browser-window mockups. Not used in the product app.
- **sh-xl** (`0 24px 60px rgba(16,24,40,.14), 0 8px 20px rgba(16,24,40,.06)`): Modal overlays, hero login card, floating panels requiring maximum elevation signal.

### Named Rules

**The Flat-by-Default Rule.** Surfaces are flat at rest. `sh-sm` is the minimum structural card lift; `sh-md` through `sh-xl` are earned through interaction or overlay context. A design that assigns `sh-lg` to a static sidebar widget is using shadow as decoration, which is prohibited.

## 5. Components

### Buttons

Buttons carry "refined precision": gently curved corners (10px radius), deliberate internal padding, and a hover lift that proves responsiveness without drama.

- **Shape:** Gently rounded (10px radius, `--r-sm`)
- **Brand (Primary CTA):** Instrument Teal to Dusk Navy gradient background (`var(--brand-grad)`), white text, `11px 18px` padding, `min-height: 44px`. The `button-brand` frontmatter token proxies `{colors.instrument-teal}` as a stand-in; the real implementation uses `var(--brand-grad)`. Box shadow: `0 4px 12px oklch(42% 0.15 270 / 0.25)`.
- **Primary (Ink):** Ink 900 fill, Warm Cream text. For secondary actions that still carry authority within the product app.
- **Ghost:** Transparent fill, 1px Warm Cream 300 border, Ink 700 text. For tertiary actions and cancel paths.
- **Hover:** Brand and Ink Primary buttons translate `-1px` on Y with an elevated shadow. Ghost fills with Warm Cream 100.
- **Focus:** 3px Instrument Teal outline, 2px offset. Must be verified for AAA contrast against each surface it appears on.
- **Size variants:** `--sm` (7px 12px padding, 12px font, 32px min-height); `--lg` (14px 22px padding, 15px font, 52px min-height). All interactive targets maintain 44px minimum height.
- **Danger variant:** Low-opacity red fill, red-tinted border, danger-red text. Used exclusively for destructive actions.

### Cards

Cards are white surfaces that float above the Warm Cream page ground. They are the only surfaces permitted to use pure white.

- **Corner Style:** Gently curved (14px radius, `--r-md`)
- **Background:** White (`#fff`)
- **Shadow:** `sh-sm` at rest. Structural, not decorative.
- **Border:** 1px Warm Cream 300. The border defines the card boundary in low-contrast contexts; the shadow provides the lift.
- **Internal Padding:** 20px uniform. Card heads use a 16px bottom margin to separate the title row from content.
- **Nesting:** Forbidden. A card inside a card is always the wrong answer. Use sections, dividers, or tinted areas instead.

### Inputs and Fields

- **Style:** White fill, 1px Warm Cream 300 border, 10px radius, `12px 14px` padding, `min-height: 46px`
- **Focus:** Border shifts to Instrument Teal Deep (`--teal-600`). Box shadow: `0 0 0 3px oklch(72% 0.11 190 / 0.2)`. The glow is a tinted ring, not a hard outer border.
- **Placeholder:** Ink 400 (`oklch(74% 0.01 260)`)
- **Error:** Border shifts to `--danger`. No glow; border color alone signals the problem.
- **Labels:** Inter 600, 13px, Ink 800. Always an explicit `<label>` element. Placeholder text is never a label.

### Navigation (Sidebar, Product App)

- **Background:** Warm Cream 100, 260px wide, sticky, full viewport height
- **Default item:** Inter 500, 14px, Ink 700, `10px 12px` padding, `--r-sm` radius
- **Hover:** Warm Cream 200 fill, Ink 900 text
- **Active:** Ink 900 fill, Warm Cream 50 text. No pseudo-element stripe. The background fill alone communicates active state. Side-stripe borders on nav items are prohibited.
- **Focus:** Instrument Teal 3px outline, 2px offset, on all items
- **Section labels:** Inter 600, 11px, Ink 500, uppercase, letter-spacing 0.08em. Non-interactive.

### Navigation (Marketing, Top Bar)

- **Background:** Warm Cream 50, 1px Warm Cream 200 border-bottom
- **Padding:** `22px 56px`, matching the hero horizontal rhythm
- **Links:** Inter 500, 14px, Ink 700; hover darkens to Ink 900 with no background fill change
- **CTA:** Brand gradient button variant

### Badges

Semantic status indicators. Pill-shaped (999px radius), compact (12px text, `3px 9px` padding), no border.

- **Success:** Low-saturation green tint (`oklch(85% 0.12 155 / 0.3)`), dark green text (`oklch(35% 0.14 155)`)
- **Warning:** Low-saturation amber tint, dark amber text
- **Danger:** Low-saturation red tint, dark red text
- **Muted:** Warm Cream 200 fill, Ink 700 text. Default for unlabeled or inactive states.

## 6. Do's and Don'ts

### Do

- **Do** set every background to the Warm Cream scale (`--cream-50` through `--cream-300`). Pure white is reserved only for card surfaces floating above the cream ground, and those cards must carry `--sh-sm`.
- **Do** reserve Instrument Serif exclusively for Display and Headline scale. Below `1.5rem`, use Inter.
- **Do** use italic Instrument Serif to mark a key phrase within a Display or Headline span, at most once per headline, with specific semantic intent.
- **Do** treat the brand gradient as a once-per-screen signal: primary CTA button and brand mark only.
- **Do** suppress all transitions and animations under `prefers-reduced-motion: reduce`. The system-level rule in `site.css` is correct; maintain it across every new component.
- **Do** pass WCAG 2.2 AAA contrast (7:1 for normal text, 4.5:1 for large text) on every new color pairing before shipping. The product is the live proof.
- **Do** write explicit `<label>` elements for every form input. Placeholder text is not a label.
- **Do** include `:focus-visible` styles on every interactive element using the 3px Instrument Teal outline.
- **Do** keep UI feedback transitions under 200ms with `cubic-bezier(0.25, 1, 0.5, 1)`. Reveal/expand animations may use up to 300ms.
- **Do** animate only `transform` and `opacity`. Never animate layout properties.

### Don't

- **Don't** use `background-clip: text` combined with a gradient background. Gradient text is prohibited. The current hero title `<span>` and features headline `<span>` in `site.css` use this pattern and must be replaced with a solid color (Dusk Navy `oklch(42% 0.15 270)`) or italic Instrument Serif without color change.
- **Don't** use a `border-left` or `border-right` wider than 1px as a colored accent stripe on any card, list item, callout, or navigation element. The active nav item `::before` pseudo-element (3px wide, brand-gradient fill) in the current codebase is this pattern and must be removed; the Ink 900 background fill communicates active state alone.
- **Don't** use emoji as icons in primary UI surfaces. Feature cards with emoji icons undercut enterprise credibility. Replace with SVG icons from a consistent icon set.
- **Don't** place the brand gradient on large background areas or section washes. Restrained means it covers at most 10% of any given screen.
- **Don't** use neon colors, rainbow gradients, or high-chroma fills outside the defined semantic tokens. Playfulness signals immaturity to enterprise buyers.
- **Don't** produce a cold, clinically white layout. Every background must pass the Parchment Test.
- **Don't** use dark mode as the primary theme. Warm Cream light is the identity; dark variants are secondary.
- **Don't** apply `box-shadow` to static surfaces that don't change elevation state. Shadows respond to interaction; `sh-sm` on cards is the only structural exception.
- **Don't** nest cards. A card inside a card is always wrong. Use section dividers, background tint areas, or plain prose groupings instead.
- **Don't** use the hero-metric template: big number, small label, supporting stat row, gradient accent. It is a SaaS cliche that signals generic, not enterprise-grade.
