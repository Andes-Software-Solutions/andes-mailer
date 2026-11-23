# UI design system

The UI looks the same on every screen, in light and dark, because every colour, font, and shape comes from one place. This page explains where that place is, what it holds, and how to add to it without breaking the match with the design. The source of truth for how things should look is the mockup, `docs/mockups/design-system.html`; the code in `andes-mailer-ui/` is that mockup translated onto Bootstrap 5.3. For the app shell that uses it, see [Users and permissions](users-and-permissions.md#how-the-ui-uses-permissions) and [Authentication](authentication.md#sign-in-states).

## How it fits together

Bootstrap is the base, not a theme on top of it. The design system works in three layers, and a change belongs in the first one that can hold it:

| Layer | File | Holds |
| --- | --- | --- |
| Bootstrap Sass variables | `src/styles.scss` | The mockup's palette, type, shape, and control sizes, mapped onto Bootstrap's own variables (`$primary`, `$body-bg`, `$border-radius`, and so on), with a `-dark` twin for each colour that changes in dark mode. |
| `--am-*` tokens | `src/styles/_tokens.scss` | Mockup colours and shadows that Bootstrap has no variable for, defined once for the light theme and once for dark. |
| Mockup-only components | `src/styles/_components.scss` | The few classes Bootstrap does not have. Everything else is stock Bootstrap classes. |

`styles.scss` ends with `@use 'styles/tokens'` and `@use 'styles/components'`, so one stylesheet carries all three. Components read `--bs-*` and `--am-*` custom properties and never write a hex value, which is what lets dark mode work without a second stylesheet.

## Colour

### The palette

The mockup's green and teal palette replaces the earlier navy one. These are the roles that matter most; `styles.scss` has the complete list, including the subtle backgrounds, borders, and emphasis text colours for each theme.

| Role | Light | Dark | Notes |
| --- | --- | --- | --- |
| Primary (buttons, active states) | `#167a5d` (Green 700) | `#167a5d` (Bootstrap keeps the primary in both themes) | Not Sea Green: white text on Sea Green is 3.9:1, which passes only for large text. |
| Page background | `#f8faf9` | `#0f1f1a` | |
| Text | `#1a2b26` | `#e8f0ed` | |
| Secondary text | `#5c6f68` | `#8da59c` | |
| Border | `#e5ebe8` | `rgba(255, 255, 255, 0.08)` | |
| Headings | `#0e5772` | `#e8f0ed` | |
| Links | `#105e48` | `#6ddbb5` | |

### `--am-*` tokens

| Token | Light | Dark | Use |
| --- | --- | --- | --- |
| `--am-surface` | `#ffffff` | `#142a23` | Cards and input backgrounds |
| `--am-float` | `#ffffff` | `#1a3329` | Menus and other floating panels |
| `--am-field` | `#f3f6f5` | `#1a3329` | Quiet fills: ghost-button hover, progress track |
| `--am-border-field` | `#c9d4cf` | `rgba(255, 255, 255, 0.18)` | Borders of controls |
| `--am-sidebar`, `--am-topbar` | `#ffffff` | `#142a23` | The shell's chrome |
| `--am-icon` | `#6b7f78` | `#8da59c` | Icons only. It is 4.3:1 on white, short of the 4.5:1 text needs. |
| `--am-accent` | `#1b9270` | `#20a880` | Spinners, accent icons |
| `--am-accent-bg-2` | `#d4f5ea` | `rgba(32, 168, 128, 0.26)` | A stronger tint than `--bs-primary-bg-subtle` |
| `--am-hover` | `rgba(237, 251, 245, 0.7)` | `rgba(32, 168, 128, 0.08)` | Navigation hover |
| `--am-focus` | `#167a5d` | `#34c496` | The focus ring |
| `--am-bar` | `#1b9270` | `#1b9270` | The indeterminate progress bar |
| `--am-teal` | `#6aa49c` | `#6aa49c` | The ridgeline and hover borders |
| `--am-signin-top`, `--am-signin-bottom` | `#ffffff`, `#edfbf5` | `#0f1f1a`, `#142a23` | The sign-in page's gradient |
| `--am-shadow`, `--am-shadow-sm` | Teal-tinted | Black | Also assigned to `--bs-box-shadow` and `--bs-box-shadow-sm`, so Bootstrap's shadows follow |

### Type, shape, and size

- **Text** is Inter at 15 px on a 22 px line (`$font-size-base: 0.9375rem`); **headings** are Montserrat 600, except a page title (`h1`), which is 28 px at 700 (24 px on phones). Card and empty-state titles use `.h-card` (Inter, 16 px, 600) instead.
- **Radii:** 6 px for small controls, 8 px for buttons and inputs, 10 px for large buttons and alerts, 12 px for cards and menus, 16 px for modals.
- **Controls** are 40 px high; `btn-lg` is 48 px and `btn-sm` 32 px.

## Dark mode

`ThemeStore` (`src/app/core/theme/theme-store.ts`) is a root signal store with one piece of state, the person's `preference`: `light`, `dark`, or `system`. It resolves that to a theme and writes it to `data-bs-theme` on `<html>`, which is the attribute Bootstrap 5.3 and the `[data-bs-theme='dark']` block in `_tokens.scss` both key off.

- **System by default.** With no stored choice the store follows `prefers-color-scheme` and updates when the operating system changes.
- **Only explicit choices are stored.** Picking Light or Dark writes `localStorage['andes-mailer.theme']`; picking **Same as system** removes it, so a person who never chooses keeps following their device. Blocked storage is tolerated: the choice then lasts for the page.
- **No flash.** An inline script in the `<head>` of `src/index.html` sets `data-bs-theme` before the first paint, so the sign-in splash, which shows before the store exists, is already in the right theme even when the bundle loads slowly. It mirrors `applyInitialTheme` in `src/app/core/theme/theme-preference.ts`, which `src/main.ts` still calls before Angular boots but which alone runs too late to prevent a light flash on a slow load. Keep the script and the function in step. `index.html` carries no `data-bs-theme` attribute of its own, only the script and `<meta name="color-scheme" content="light dark">`.
- **Controls.** The top bar's toggle flips between light and dark (an explicit choice), and the account menu offers Light, Dark, and Same as system. Both call the store; after boot, nothing else sets the attribute.

To test a component in both themes, set `data-bs-theme` on a parent in the spec, or flip the toggle in the running app.

## Components

Use stock Bootstrap classes first. These are the mockup's additions, all in `src/styles/_components.scss`:

| Class | What it is |
| --- | --- |
| `btn-outline` | A neutral bordered button on the surface colour |
| `btn-ghost` | A borderless button for toolbars; hover shows `--am-field` |
| `btn-icon` (`btn-sm`) | A square 40 px (32 px) icon button; combine with `btn-ghost` |
| `badge-admin`, `badge-member` | Role badges: a solid primary fill for Administrator, a tinted outline for Member. `roleBadge()` in `core/layout/role-badge.ts` picks one. |
| `bar-indet` | An indeterminate progress bar that stops animating under `prefers-reduced-motion` |
| `avatar` (`avatar-team`, `avatar-lg`) | An initials disc, round for people and a rounded square for teams |
| `t-micro` | An uppercase 12 px section label |
| `h-card` | The card-title style above |

The same file restyles a few Bootstrap parts everywhere: one focus ring for every control, alerts that lead with an icon, `dropdown-item` and `dropdown-header`, and a lighter `btn-danger` fill in dark mode, where the standard red fails contrast.

Shared, reusable pieces in `src/app/shared/` follow the design too: `SignInLayout` (the centred card for every sign-in state), `EmptyState` (an icon in an accent disc, a title, and text), `AppLogo`, and `Ridgeline`.

### Bootstrap JavaScript

Import Bootstrap's JavaScript per component (`import { Dropdown } from 'bootstrap'`), create each instance in `afterNextRender`, and call `dispose()` on destroy. Shell code shows the three cases: `attachDropdown` for menus, `RailTooltip` for the icon rail, and `Offcanvas` for the phone menu. Bootstrap returns focus to the opener only for its data attributes, so these helpers do it themselves.

## Fonts

Inter (variable) and Montserrat (600 and 700) are self-hosted through the `@fontsource-variable/inter` and `@fontsource/montserrat` packages. `angular.json` lists their CSS ahead of `src/styles.scss`, so the browser requests the font files from the app's own origin and never from Google Fonts. To add a weight, add another `@fontsource/montserrat/<weight>.css` entry there; do not link a font from a CDN.

## Logo

Use `AppLogo` (`src/app/shared/brand/app-logo.ts`) and never an `<img>` of your own. It renders the official Andes mark: `public/andes-mark.png` in the light theme and `public/andes-mark-white.png` in the dark one (it swaps by CSS, with no script). It replaces the mockup's stand-in mark.

| Input | Default | Meaning |
| --- | --- | --- |
| `height` | 32 | The mark's height in pixels; the width follows the mark's 663 by 264 proportions |
| `wordmark` | `true` | Shows "Andes" and "Software Solutions" beside the mark |
| `product` | `Mailer` | The product name in the accent colour; set it to empty for the company wordmark |
| `priority` | `false` | Set where the logo is the page's largest image, such as the sign-in card |

The logo is decorative (`alt=""`); the link or heading around it carries the name. Do not recolour, crop, or stretch the files.

## Accessibility deviations from the mockup

Where the mockup fell short of WCAG 2.2 AA, the code departs from it on purpose:

- **Focus ring.** The mockup's light ring, `#34c496`, is 2.2:1 on white, short of the 3:1 a focus indicator needs. The light theme uses Green 700 (`#167a5d`); dark keeps `#34c496`. `--am-focus` holds the value.
- **Placeholders.** The mockup's `#9bada5` is 2.4:1. Placeholders use the secondary text colour (`$input-placeholder-color: var(--bs-secondary-color)`) for 4.5:1.
- **Primary.** The primary colour is Green 700, not the mockup's brighter Sea Green, because white text on Sea Green is 3.9:1.
- **Tertiary text.** `$body-tertiary-color` (`#9bada5`) is for decoration only. Never use it for text.
- **Icons.** `--am-icon` is for icons only, since it is below 4.5:1 on white.

Check any new text colour at 4.5:1 and any new control border or focus indicator at 3:1, in both themes.

## Add a colour

1. **Does Bootstrap have a variable for it?** If so, set it in the `with (...)` list in `src/styles.scss`, and set its `-dark` twin if dark mode needs a different value (`$link-color` and `$link-color-dark`). Every component that uses the Bootstrap variable follows.
2. **If not, add a token.** Add `--am-<name>` to both the `:root, [data-bs-theme='light']` block and the `[data-bs-theme='dark']` block in `src/styles/_tokens.scss`. A token with only one value breaks the other theme.
3. **Use it through the variable** (`var(--am-name)` or `var(--bs-primary)`), never as a hex value in a component. A comment that says why helps: the contrast numbers above are in the code for the same reason.
4. **Check contrast** in light and dark, and look at the result with the theme toggle.

## Add a component

1. **Look for a Bootstrap class first.** Cards, alerts, badges, buttons, dropdowns, and offcanvas all exist and already follow the palette.
2. **A style the mockup adds and several features will use** goes in `src/styles/_components.scss`, built from variables, with a comment naming the mockup piece. Add a row to the table above.
3. **A style for one component** goes in that component's `styleUrl` or `styles`, reading the same variables. The production build warns at 4 kB and fails at 8 kB per component stylesheet (`anyComponentStyle`).
4. **A reusable piece with markup** goes in `src/app/shared/` and takes content through inputs or projection, with no business logic and no `SessionStore`; see `angular-ui-architecture` for the layer rules.
5. **Check it in both themes, at phone, tablet, and desktop widths,** and with the keyboard: the focus ring comes from the shared `:focus-visible` rule, so do not remove outlines.

## Bundle size

The shell and home page are lazy-loaded, which keeps Bootstrap's JavaScript (Dropdown, Offcanvas, Tooltip) out of the bundle the sign-in screens need; `accessGuard` starts fetching the shell while the account loads, so there is no extra wait afterwards. The initial bundle is about 930 kB raw and 185 kB transferred, and the production budget is 1 MB warning, 1.2 MB error. A new global import, a font weight, or an eager route counts against it; run `npx ng build` and read the budget output before adding one.
