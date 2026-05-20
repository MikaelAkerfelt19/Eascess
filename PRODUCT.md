# Product

## Register

brand

## Users

Large enterprises, corporate IT managers, and compliance officers evaluating or actively managing WCAG 2.2 compliance across their web properties. When they land on the marketing site or watch the widget demo, they are making a risk assessment: "Will this break our site? Can we trust this vendor with our production environment?" They need to feel that Eascess is rock-solid, technically serious, and worth injecting into a multi-million dollar website. They are not browsers — they are decision-makers under compliance pressure.

## Product Purpose

Eascess provides web accessibility as a managed service: a single JavaScript snippet that delivers a WCAG 2.2-compliant accessibility widget, automated AI-powered alt-text generation, continuous compliance scoring, and a multi-domain management dashboard. The product exists to eliminate the gap between "we know we need to be compliant" and "we actually are." Success looks like an enterprise IT manager approving the widget after a single demo, confident it will not slow down or interfere with their site.

## Brand Personality

Reliable, Fluid, Intuitive. The voice is precise and assured — not flashy, not bureaucratic. Stripe- and Vercel-grade B2B polish: every pixel earns trust before a word is read. Emotionally, the interface should make enterprise visitors feel that Eascess has already solved the hard problem, and all they need to do is add one line of code.

## Anti-references

- No neon colors, rainbow gradients, or consumer-startup vibrancy. Playfulness signals immaturity to enterprise buyers.
- No dark-mode-only aesthetics. The Warm Cream system is the identity; dark variants are secondary.
- No cold, sterile government-portal flatness. White + grey + blue-link compliance tools look like the problem, not the solution.
- No emoji-as-icons in primary UI surfaces. Feature cards with emoji icons undercut enterprise credibility.
- No side-stripe border accents on cards or list items (this is a design anti-pattern, not just a brand one).
- No gradient text (background-clip: text). Use solid color. Emphasis through weight or size.

## Design Principles

1. **Practice what you preach.** Every color pair, interactive element, and text size must pass WCAG 2.2 AAA natively. The marketing site is the live proof-of-concept. An accessibility product that fails its own audit is disqualifying.

2. **Earned confidence.** Visual quality comes from precision, finish, and restraint — not decoration. The Warm Cream palette, Instrument Serif display type, and tight spacing communicate authority. Ornament should be earned, never default.

3. **Warm authority.** Professionalism does not require coldness. The cream surfaces and serif type give the brand warmth and approachability without sacrificing seriousness. Enterprise-grade and human-scale can coexist.

4. **Motion as signal, not performance.** Every transition communicates state (loading, success, focus, expand). Nothing animates for visual interest alone. All motion respects `prefers-reduced-motion` and stays under 200ms for UI feedback.

5. **Invisible complexity.** Eascess handles WCAG audits, Shadow DOM isolation, dynamic CORS, and AI inference. None of this complexity should surface in the interface. The experience should make the hardest accessibility problem feel like adding one line of code.

## Accessibility & Inclusion

Target: WCAG 2.2 AAA. Non-negotiable baseline for all marketing and product surfaces.

- Color contrast: all text/background pairs must meet AAA (7:1 for normal text, 4.5:1 for large text minimum — target higher where possible within the Warm Cream palette).
- Focus rings: visible, high-contrast, AAA-passing focus indicators on every interactive element. The teal focus ring (`oklch(72% 0.11 190)`) must be checked against each surface it appears on.
- Keyboard navigation: full keyboard operability across all interactive components, including the widget itself and the dashboard.
- Screen readers: semantic HTML, correct heading hierarchy, descriptive ARIA labels on icon-only controls, live regions for dynamic content.
- Motion: all animations and transitions must be suppressed or replaced with instant state changes under `prefers-reduced-motion: reduce`.
- Language: primary UI is Turkish; all `lang` attributes must be set correctly for screen reader pronunciation.
