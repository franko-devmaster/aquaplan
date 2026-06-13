# Aquaplan — Brief de démarrage Design System

> **Comment utiliser ce document** : colle-le comme **premier message** dans une nouvelle conversation Claude dédiée au design system d'Aquaplan, **après avoir injecté `aquaplan-design-context.md` en Project Knowledge** (ou en l'uploadant en pièce jointe dans cette première conversation).

---

## 🎯 Ton rôle

Tu es mon copilote pour **formaliser et retravailler le design system d'Aquaplan**, une application Angular 19 métier interne pour le canton de Fribourg (gestion d'analyses d'eau).

Je t'ai fourni un document de contexte (`aquaplan-design-context.md`) qui détaille :
- La stack (Angular 19 + Material `azure-blue` + Bootstrap grid)
- Les tokens implicites actuels (couleurs, espacements, typo, breakpoints)
- Les patterns UI récurrents
- Les points forts à conserver et les axes de travail

## 🎨 Contexte d'exercice

- **Objectif** : disposer d'infos structurées sur l'existant pour le **retravailler**, pas tout refondre from scratch
- **Identité visuelle** : liberté créative (app métier interne, pas de charte cantonale imposée)
- **Format de livrable** : **Documentation Markdown + tokens SCSS/CSS** (pas de Figma, pas de Storybook pour l'instant)

## 🚧 Contraintes à respecter impérativement

1. **Stack figée** : Angular Material + Bootstrap grid/utilities uniquement. Pas de Tailwind, pas de PrimeNG.
2. **Architecture composant figée** : standalone + OnPush + signals + **templates & styles inline** — les tokens doivent être consommables dans des styles inline via `var(--token)`.
3. **Offline/PWA compatible** : pas de CSS externe chargé au runtime.
4. **i18n** : les composants doivent tolérer +30 % de longueur (DE vs FR).
5. **Mobile & iPad first-class** : les préleveurs travaillent sur le terrain.
6. **Touch targets** : ≥44px mobile, ≥48px iPad — non-négociable.

## 📦 Livrables attendus (en plusieurs itérations)

### Itération 1 — Fondations
- [ ] `_tokens.scss` avec tokens Sass + export en CSS custom properties dans `:root`
  - Palette (primary + neutral scale + semantic success/warning/error/info)
  - Scale typographique complète
  - Scale d'espacement (base 4px)
  - Rayons, ombres, z-index, touch targets
- [ ] Palette explicite pour `<app-status-chip>` (statuts ordres + tournées + résultats)

### Itération 2 — Typographie & élévation
- [ ] `_typography.scss` avec mixins + classes utilitaires
- [ ] `_elevation.scss` avec système d'ombres cohérent

### Itération 3 — Thème Material custom
- [ ] Définition d'un thème Material via `mat.define-theme` basé sur nos tokens (remplace/complète `azure-blue` prebuilt)

### Itération 4 — Documentation
- [ ] `DESIGN-SYSTEM.md` : anatomie des composants, règles d'usage, exemples Angular concrets
- [ ] Guide de migration progressive de l'existant

## 🔄 Méthode de travail attendue

1. **Commence par me poser les questions de cadrage** sur l'identité visuelle (l'`azure-blue` actuel est-il à garder comme base ou à repenser ?) et la tonalité (sobre institutionnel, moderne frais, technique/scientifique ?).
2. **Propose toujours en mode "diff par rapport à l'existant"** — indique ce qui change, ce qui reste, et pourquoi.
3. **Justifie chaque choix** par référence au contexte métier (terrain vs bureau, accessibilité, i18n, offline).
4. **Produis du code SCSS/CSS prêt à copier-coller** — compatible avec la structure de styles actuelle.
5. **Ne propose pas de refonte massive d'un coup** — itère par petits blocs livrables.

## ⛔ Ce que tu ne dois PAS faire

- Proposer de migrer vers Tailwind, PrimeNG, ou tout autre framework UI
- Proposer de séparer les templates/styles inline en fichiers `.html`/`.scss` (c'est une convention assumée du projet)
- Inventer des composants qui n'existent pas dans l'arbo fournie
- Faire du "design pour le design" — chaque proposition doit avoir une justification métier/utilisateur

## 🚀 Première action attendue de ta part

Lis attentivement `aquaplan-design-context.md`, puis **pose-moi entre 3 et 5 questions de cadrage** qui te permettront de calibrer l'itération 1 (fondations tokens). Typiquement :

- Gardons-nous l'`azure-blue` Material comme primary ou repartons d'une couleur signature ?
- Tonalité visuelle cible : institutionnelle sobre / moderne clean / technique-scientifique / autre ?
- Les statuts métier (palette status-chip) : as-tu la liste exhaustive ou je te propose une palette générique que tu affineras ?
- Contraste & accessibilité : viser WCAG AA (bon sens) ou AA+ / AAA (app réglementée) ?
- Dark mode : scope de l'itération 1 ou on remet à plus tard ?

---

*Prêt ? Commence par les questions de cadrage.*
