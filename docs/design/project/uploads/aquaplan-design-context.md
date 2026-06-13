# Aquaplan — Contexte Design System (état actuel)

> **Objectif de ce document** : fournir à Claude (ou tout designer/IA de design) une vision complète et structurée du design system *implicite* actuel d'Aquaplan, afin de servir de base à sa refonte, son formalisation ou son extension.
>
> **Projet** : Aquaplan — application web de gestion digitale des ordres et prescriptions d'analyses sur les eaux potables et de baignade du canton de Fribourg (LIMS, tournées de prélèvement, résultats).
> **Repo** : `francisuster/aquaplan` (Bitbucket, branche `main`)
> **Mandat** : Softcom Technologies pour le canton de Fribourg.

---

## 1. Contexte métier & utilisateurs

### 1.1 Domaine fonctionnel

Application de gestion d'un workflow de **laboratoire d'analyses d'eau** :

- Création et suivi d'**ordres d'analyse** (prescriptions)
- Planification de **tournées de prélèvement** (sampling rounds)
- Gestion de **lieux de prélèvement** (sampling locations) et **distributeurs d'eau** (communes, syndicats)
- Catalogue de **programmes d'analyse** (quels paramètres mesurer)
- Saisie terrain des **prélèvements** (mobile/tablette)
- Consultation des **résultats d'analyse** (LIMS)
- Administration (utilisateurs, délégations, secteurs)

### 1.2 Profils utilisateurs

| Profil | Contexte d'usage | Device dominant |
|---|---|---|
| **Agents LIMS / labo** | Bureau, gestion ordres et résultats | Desktop |
| **Préleveurs terrain** | Sur le terrain, extérieur, tournées | Tablette iPad, mobile |
| **Responsables** | Planification, validation, KPIs | Desktop |
| **Distributeurs** (communes) | Consultation ordres et résultats les concernant | Desktop + mobile |
| **Administrateurs** | Paramétrage, délégations | Desktop |

### 1.3 Contraintes métier fortes

- **Multilingue** FR / DE / EN (Suisse romande + alémanique + tourisme)
- **Offline-first** pour les préleveurs terrain (IndexedDB via `idb`, PWA, service-worker)
- **Scan de codes-barres** (étiquettes d'échantillons) via `@zxing`
- **Traçabilité réglementaire** forte (LIMS = environnement régulé)
- **Identité visuelle** : app métier interne, **liberté créative** (pas de charte cantonale imposée)

---

## 2. Stack technique

### 2.1 Framework & librairies UI

| Couche | Choix | Version | Rôle |
|---|---|---|---|
| Framework | Angular (standalone, OnPush) | 19.2 | Architecture composants |
| Design kit principal | Angular Material | 19.2 | Composants UI (dialog, form-field, table, chip, etc.) |
| Thème Material | `azure-blue` (prebuilt) | — | Palette primaire bleue |
| Grid & utilitaires | Bootstrap (grid + utilities only) | 5.3 | Layout responsive ponctuel |
| Icônes | Material Icons | — | Iconographie exclusive |
| Typographie | Roboto, Helvetica Neue, sans-serif | — | Body + headings |
| i18n | `@ngx-translate/core` | 17 | FR/DE/EN |
| Offline | `idb` (IndexedDB) | 8 | Cache local |
| PWA | `@angular/service-worker` | 19.2 | Mode déconnecté |
| Barcode | `@zxing/browser` + `@zxing/library` | — | Scan étiquettes |

### 2.2 Patterns d'architecture UI

- **100 % standalone components** (pas de `NgModule`)
- **ChangeDetectionStrategy.OnPush** systématique
- **Signals** pour l'état local (`signal`, `computed`)
- **Templates inline** + **styles inline** — **pas de fichiers `.html` ou `.scss` séparés** par composant
- Control flow moderne : `@if`, `@for`, `@else` (pas de `*ngIf` / `*ngFor`)
- **Datastores signal-based** (pattern custom type `OrderDatastore`)
- **ReactiveFormsModule** pour formulaires complexes (dialogs), `FormsModule` (ngModel) pour formulaires simples (login)

---

## 3. Design tokens implicites (actuels)

> Les tokens ci-dessous sont **extraits par rétro-ingénierie** du code existant. Ils ne sont **pas formalisés** dans un fichier dédié et sont dispersés entre `styles.scss`, `_responsive.scss`, et les styles inline des composants. **C'est précisément ce qu'il faut formaliser.**

### 3.1 Couleurs

| Token proposé | Valeur actuelle | Usage observé | Source |
|---|---|---|---|
| `--color-primary` | (Material `azure-blue` — ~`#0074D9` / `#005DB0`) | Boutons primaires, liens, accents | Thème Material prebuilt |
| `--color-error` | `#f44336` | Messages d'erreur login | `login.component.ts` |
| `--color-text-muted` | `#666` | Labels secondaires, info-text | `_responsive.scss`, dialogs |
| `--color-text-label` | `#666` | Labels uppercase de sections | `_responsive.scss` |
| `--color-bg-page` | `#f5f5f5` | Fond écran login | `login.component.ts` |
| `--color-bg-card` | `#fff` | Fond cartes, rows mobile | `_responsive.scss` |
| `--color-border-default` | `#e0e0e0` | Bordures cartes mobile, sections | `_responsive.scss`, dialogs |
| `--color-border-subtle` | `#f0f0f0` | Séparateurs internes mobile | `_responsive.scss` |
| `--color-row-hover` | `rgba(0,0,0,0.04)` | Hover lignes tableau cliquables | `_responsive.scss` |
| `--color-row-active` | `rgba(0,0,0,0.08)` | Active (touch) lignes mobile | `_responsive.scss` |
| `--color-shadow-card` | `rgba(0,0,0,0.08)` | Ombre cartes mobile | `_responsive.scss` |

**⚠️ Observations critiques** :
- Pas de palette sémantique formalisée (success, warning, info)
- Pas de scale neutre cohérente (gris utilisés en dur : `#666`, `#e0e0e0`, `#f0f0f0`, `#f5f5f5`)
- Couleur primaire hérite entièrement du thème Material (pas de choix explicite)
- Pas de dark mode prévu
- **Les statuts** (ordres, tournées) sont gérés via un composant `<app-status-chip>` dédié — la palette de statuts n'est pas documentée ici et mérite un relevé complet

### 3.2 Typographie

| Token proposé | Valeur actuelle | Usage |
|---|---|---|
| `--font-family-base` | `Roboto, "Helvetica Neue", sans-serif` | Body global |
| `--font-size-base` | `16px` (mobile forcé) | Body mobile (évite zoom iOS) |
| `--font-size-h2-mobile` | `20px` | Page headers mobile |
| `--font-size-td-mobile` | `16px` | Cellules tableaux mobile |
| `--font-size-label-mobile` | `13px` | Labels ::before en mode cards mobile |
| `--font-size-section-label` | `12px` | Labels uppercase sections |
| `--font-size-paginator-mobile` | `14px` | Paginator compact mobile |
| `--font-size-info` | `13px` | Texte info italique |
| `--font-weight-label` | `500` / `600` | Labels (500 section, 600 cards mobile) |
| `--line-height-td-mobile` | `1.4` | Cellules mobile |

**⚠️ Observations** :
- Pas de scale typographique formalisée (pas de `h1, h2, h3, body, caption` définis globalement)
- Beaucoup de `font-size` en dur dans les styles inline
- Pas de tokens pour `font-weight` (dispersés : 500, 600, défaut)
- `letter-spacing` / `text-transform` utilisés ponctuellement (`uppercase` sur labels de section)

### 3.3 Espacements

| Valeur observée | Usage |
|---|---|
| `4px` | (rare, fine tuning) |
| `8px` | Gap form fields, margin-bottom form-field, gap filters |
| `12px` | Padding cards mobile (vertical et side), gap page-header, gap round-mode-group, margin-bottom rows cards, padding round-section |
| `16px` | Padding cards mobile horizontal, margin-bottom error |
| `24px` | Padding login-card, margin divider |

**Scale implicite** : `4-8-12-16-24` — base 4px, cohérent mais non formalisé.

**⚠️ Observations** :
- Pas de tokens d'espacement définis (`--space-xs`, `--space-sm`, etc.)
- Certaines valeurs hybrides (`8px 16px`, `10px`, `12px 16px`) trahissent des micro-ajustements au cas par cas
- Pas de système de `gap` formalisé pour les layouts flex/grid

### 3.4 Rayons de bordure

| Valeur | Usage |
|---|---|
| `0` (login-card mobile, dialogs fullscreen mobile) | Bordless en fullscreen |
| `8px` | Cartes mobile (rows), round-section |

**⚠️ Observations** :
- Un seul rayon non-zéro utilisé (`8px`)
- Pas de token `--radius-sm / -md / -lg`

### 3.5 Ombres

| Valeur | Usage |
|---|---|
| `0 1px 3px rgba(0,0,0,0.08)` | Cartes mobile (rows) |

**⚠️ Observations** :
- Une seule ombre custom définie
- Reste des ombres héritées de Material (dialog, card, menu)
- Pas d'élévation formalisée

### 3.6 Breakpoints (le seul système bien formalisé ✅)

```scss
$breakpoint-mobile: 768px;   // <768 = mobile
$breakpoint-ipad:   1024px;  // 768-1023 = iPad portrait
$breakpoint-tablet: 1200px;  // 768-1199 = tablet
                             // ≥1200 = desktop
```

**Mixin `@include responsive($size)` supporte** :
- `mobile` — `<768px`
- `ipad` — `768–1023px`
- `tablet` — `768–1199px`
- `mobile-ipad` — `<1024px`
- `mobile-tablet` — `<1200px`
- `desktop` — `≥1200px`

**✅ Point fort** : système propre, pensé pour l'usage terrain (iPad des préleveurs) — **à conserver tel quel** dans le futur design system.

### 3.7 Tailles tactiles (touch targets)

| Contexte | Min-height/width observé |
|---|---|
| Icon buttons mobile | `44×44px` (cellules actions) |
| Icon buttons iPad | `48×48px` (override explicite) |
| Boutons raised/outlined iPad | `min-height: 44px` |
| Lignes tableau iPad | `min-height: 56px` |
| Boutons page-header mobile | `min-height: 44px`, `width: 100%` |
| Paginator mobile | `40×40px` |

**✅ Point fort** : tailles tactiles respectent les guidelines (≥44px Apple HIG, ≥48px Material). À formaliser en tokens `--touch-target-sm/md`.

---

## 4. Patterns UI identifiés

### 4.1 Pattern "Formulaire dans dialog"

Convention récurrente (exemple : `order-create-dialog.component.ts`) :

```
<h2 mat-dialog-title>Titre</h2>
<mat-dialog-content>
  <form [formGroup]="form" class="form-container">
    <!-- mat-form-field avec appearance="outline" + class="full-width" -->
    <!-- Sections optionnelles encadrées (round-section) -->
    <!-- Contenu conditionnel via @if -->
  </form>
</mat-dialog-content>
<mat-dialog-actions align="end">
  <button mat-button mat-dialog-close>Annuler</button>
  <button mat-raised-button color="primary" (click)="onSubmit()">Valider</button>
</mat-dialog-actions>
```

**Styles récurrents** :
```scss
.form-container { display: flex; flex-direction: column; min-width: 450px; gap: 8px; }
.full-width { width: 100%; }
```

**Responsive** : classe utilitaire `.responsive-dialog` qui transforme en fullscreen en mobile.

### 4.2 Pattern "Liste paginée filtrable"

Convention (exemple : `order-list.component.ts`) :

- `<h2>` page header avec bouton action à droite (classe `.page-header`)
- Rangée de filtres au-dessus du tableau (classe `.filters-row`)
- `<mat-table>` Material avec classe conteneur `.responsive-table-container` (pour cards mobile)
- Lignes cliquables : `.clickable-row` (hover + active touch)
- Paginator en bas
- Composant `<app-status-chip>` pour statuts (dans cellule dédiée)

**Responsive mobile** : tableaux → cards verticales via CSS pur (attribut `data-label` sur chaque `<td>` pour afficher le nom de colonne en pseudo-élément).

### 4.3 Pattern "Carte de login"

Exemple : `login.component.ts`
- Container flex centré, hauteur 100vh, fond `#f5f5f5`
- `<mat-card>` 400px width, padding 24px
- Form-fields outline full-width avec icône prefix
- Bouton primaire full-width
- Divider + bouton SSO optionnel stroked

**Responsive mobile** : `.login-card` passe en fullscreen (100vw / 100vh).

### 4.4 Pattern "Section conditionnelle encadrée"

Observé dans `order-create-dialog` (bloc `round-section`) :
- Bordure `1px solid #e0e0e0`
- Radius `8px`
- Padding `12px`
- Label de section en petit, uppercase, couleur atténuée
- Contenu conditionnel via `@if` selon radio-group

### 4.5 Pattern "Status chip"

Composant dédié `<app-status-chip>` (voir `components/status-chip/status-chip.component.ts`) — **le relevé exhaustif des variantes de statut et de leur palette n'est pas couvert ici** et devrait être fait en priorité.

Statuts métier connus :
- **Ordres** : à planifier, planifié, en cours, prélevé, en analyse, résultats, clôturé, annulé
- **Tournées** : Draft, Assigned, (… autres à relever)

---

## 5. Composants existants recensés

> Extrait de l'arborescence `src/app/components/`

| Composant | Rôle probable |
|---|---|
| `barcode-scanner` | Scan de codes-barres échantillons (zxing) |
| `footer` | Pied de page |
| `header` | En-tête avec nav + user menu |
| `layout` | Squelette (header + sidebar + router-outlet) |
| `notifications-bell` | Cloche notifications |
| `sampling-results-table` | Table spécialisée résultats d'analyse |
| `sidebar` | Navigation latérale |
| `status-chip` | Chip de statut colorée |
| `confirm-dialog` | Dialog de confirmation générique |

---

## 6. Utilitaires CSS globaux disponibles

| Classe | Effet |
|---|---|
| `.hide-mobile` | `display: none` en `<768px` |
| `.show-mobile-only` | Visible uniquement en `<768px` |
| `.responsive-table-container` | Transforme `<mat-table>` en cards en mobile |
| `.responsive-dialog` | Dialog fullscreen en mobile |
| `.form-container` | Flex column, min-width 450px, gap 8px |
| `.full-width` | `width: 100%` |
| `.page-header` | Header de page (responsive flex-direction) |
| `.filters-row` | Barre de filtres (responsive flex-direction) |
| `.clickable-row` | Row tableau cliquable (hover + active) |
| `.login-card` | Card de login (responsive fullscreen) |
| `.status-chip` | (Sur chip Material) désactive interactivité |

---

## 7. Diagnostic — points forts & axes de travail

### ✅ Points forts à conserver

1. **Système de breakpoints propre et bien nommé** (`_responsive.scss`) — la structure du mixin `responsive($size)` est claire et couvre tous les cas (mobile / iPad / tablet / desktop / combinés).
2. **Prise en compte sérieuse de l'usage terrain** : touch targets ≥44px, iPad optimisé à 48px, tableaux → cards en mobile, dialogs fullscreen mobile, font-size 16px pour éviter le zoom iOS.
3. **Pattern "table responsive → cards"** via CSS pur (`data-label` + `::before`) — élégant et maintenable.
4. **Cohérence architecturale** : 100% standalone + OnPush + Signals + inline templates/styles — discipline à préserver.
5. **Palette de base Material** : choix raisonnable pour un MVP, donne une cohérence immédiate.

### 🔧 Axes de travail prioritaires pour le design system

1. **Formaliser les tokens** dans un fichier dédié (`_tokens.scss` + custom properties CSS) :
   - Palette complète (primary, neutral scale, semantic : success/warning/error/info)
   - Scale typographique (h1→caption + body variants)
   - Scale d'espacement (`--space-1` à `--space-12`, base 4px)
   - Rayons (`--radius-sm/md/lg`)
   - Ombres (`--elevation-1` à `--elevation-4`)
   - Touch targets (`--touch-sm: 40px`, `--touch-md: 44px`, `--touch-lg: 48px`)
   - **Z-index scale** (non traitée aujourd'hui)
2. **Définir la palette de statuts** (status-chip) de façon explicite — crucial vu le domaine métier (workflow).
3. **Scale typographique** à formaliser : actuellement beaucoup de `font-size` dispersés.
4. **Éliminer les valeurs en dur** dans les styles inline (migrer vers custom properties consommées depuis `:root`).
5. **Documenter les patterns** comme composants réutilisables :
   - `DialogForm` (pattern dialog + form)
   - `PageHeader` (titre + action)
   - `FiltersRow` (barre filtres responsive)
   - `DataTable` (table responsive avec cards mobile)
   - `ConditionalSection` (section encadrée avec label + contenu conditionnel)
6. **Dark mode** à envisager (absent aujourd'hui).
7. **Accessibilité** : à auditer — contrastes, focus visibles, ARIA sur chips cliquables/non-cliquables, labels formulaires.
8. **Iconographie** : 100 % Material Icons aujourd'hui — valider ce choix ou introduire un set métier custom (ex. icônes spécifiques pour types de prélèvement).

---

## 8. Livrables attendus du design system

Pour la phase suivante (retravail avec Claude Design), les livrables cibles sont :

1. **Fichier `_tokens.scss`** : tokens Sass + CSS custom properties
2. **Fichier `_typography.scss`** : mixins et classes utilitaires typographiques
3. **Fichier `_elevation.scss`** : système d'ombres
4. **Refonte de `_responsive.scss`** : à consommer les nouveaux tokens (les mixins existants sont à conserver)
5. **Documentation Markdown** : `DESIGN-SYSTEM.md` avec anatomie des composants, règles d'usage, exemples de code Angular
6. **Guide de migration** : comment remplacer les valeurs en dur par les tokens dans l'existant (sans tout casser)

**Format final** : Documentation Markdown + tokens SCSS/CSS (Figma / Storybook hors scope pour l'instant).

---

## 9. Contraintes techniques à respecter

- ⚠️ **Ne pas introduire de dépendances UI supplémentaires** (pas de Tailwind, pas de PrimeNG, pas de composants headless type Radix). On reste sur **Angular Material + Bootstrap grid/utilities**.
- ⚠️ **Ne pas changer la structure des composants standalone + inline templates/styles** — les tokens doivent être consommables depuis les styles inline via `var(--token)`.
- ⚠️ **Préserver la compatibilité PWA offline** — pas de chargement de CSS runtime externe.
- ⚠️ **Thème Material `azure-blue`** : à décider s'il est conservé, customisé (via `mat.define-theme`), ou remplacé par un thème personnalisé. Recommandation : définir un thème custom basé sur les tokens pour découpler.
- ⚠️ **i18n** : les longueurs de texte varient fortement (DE plus long que FR) — les composants doivent tolérer jusqu'à +30 % de longueur de label.

---

## 10. Annexe — extraits de code de référence

Les extraits suivants sont représentatifs des conventions actuelles et doivent servir de **baseline** pour toute proposition de refonte :

### 10.1 `styles.scss` (racine, très minimaliste)

```scss
@use '@angular/material' as mat;
@use 'styles/responsive' as *;

html, body { height: 100%; }
body {
  margin: 0;
  font-family: Roboto, "Helvetica Neue", sans-serif;
}
```

### 10.2 `angular.json` — ordre de chargement des styles

```
1. @angular/material/prebuilt-themes/azure-blue.css
2. bootstrap/dist/css/bootstrap-grid.min.css
3. bootstrap/dist/css/bootstrap-utilities.min.css
4. src/styles.scss  (hérite et surcharge)
```

### 10.3 Typage d'un composant standard (baseline à respecter)

```typescript
@Component({
  selector: 'app-xxx',
  standalone: true,
  imports: [ /* seulement ce qui est utilisé */ ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` ... `,           // inline
  styles: [` ... `],            // inline
})
export class XxxComponent {
  private readonly svc = inject(SomeService);
  readonly data = signal<T[]>([]);
  readonly filtered = computed(() => /* ... */);
}
```

---

## 11. Ce qui manque dans ce document (et qu'il faudrait fournir en complément)

Pour pousser plus loin, voici ce qu'il serait utile d'ajouter en phase 2 :

- [ ] Le contenu complet de `status-chip.component.ts` (palette des statuts)
- [ ] Le contenu de `header.component.ts` et `sidebar.component.ts` (structure navigation)
- [ ] Le contenu de `layout.component.ts` (squelette d'app)
- [ ] 2-3 **screenshots** représentatifs (desktop + iPad + mobile) — c'est ce qui manquerait pour juger du "vibe" visuel
- [ ] Exemple de page **liste de résultats** (`results/`) — pour valider le pattern DataTable sur un cas complexe
- [ ] Exemple de page **saisie terrain** (prélèvement avec scan) — pour valider l'UX mobile
- [ ] La charte de couleurs des **statuts métier** (ordres, tournées, résultats) telle qu'elle existe aujourd'hui
- [ ] Le contenu de `styles.scss` final (si plus riche que ce qui est documenté)

---

*Document généré le 21 avril 2026 à partir du contexte frontend fourni (branche `main`, repo `francisuster/aquaplan`). À injecter en **Project Knowledge** dans une conversation Claude dédiée au design system pour un travail itératif.*
