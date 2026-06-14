# Audit UX/UI — AquaPlan (hiérarchie d'action, boutons, typographie, responsive)

> Audit demandé suite au constat : sur l'écran Tournée, les boutons **Supprimer** et
> **Annuler la tournée** sont trop proéminents alors que ce ne sont pas les actions principales.
> Objectif : corriger ce **type** de problème de façon **cohérente sur tous les écrans**.
>
> Statut : **rapport à valider** avant application. Captures de référence dans
> `tests/e2e/reports/screenshots/audit_*.png`.

## 1. Principes (rubrique à appliquer partout)

1. **Une seule action primaire par écran.** Bouton plein, couleur primaire, position
   constante (proposé : **à droite** de la barre d'actions). Tout le reste est secondaire.
2. **Actions secondaires** = `mat-stroked-button` neutre (sans couleur d'appel).
3. **Actions destructives** (Supprimer, Annuler, Rejeter, Forcer) :
   - **jamais** dans la rangée primaire, **jamais** en bouton plein rouge ;
   - reléguées dans un **menu overflow « ⋮ »** (écrans de détail) ou en `mat-stroked-button`
     discret **à l'extrême droite**, séparées des actions positives ;
   - confirmation obligatoire (déjà en place via les dialogs de confirmation).
4. **Pas de primaires concurrentes** : ne pas mélanger `color="primary"` et `color="accent"`
   en boutons pleins sur le même écran (choisir 1 primaire, le reste secondaire).
5. **Échelle typographique unique** (tokens `_tokens.scss`) : titres de page, sous-titres,
   labels, boutons — mêmes tailles/poids d'un écran à l'autre.
6. **Cibles tactiles ≥ 44px** en mobile ; libellés de boutons jamais tronqués.
7. **Patterns cohérents** : filtres empilés en mobile, tableaux → cartes en mobile (déjà OK),
   dialogs avec actions `[Annuler] [Action primaire]` en bas à droite.

## 2. Constats globaux (relevés par scan du code, toutes pages)

| Constat | Où | Sévérité |
|---|---|---|
| Action **destructive en bouton plein rouge** (`mat-raised-button color="warn"`) | `orders` (confirmDelete), `sampling-locations` (onDelete) | Élevée |
| Actions destructives **en tête, avant l'action primaire** | `sampling-rounds` (détail) : Supprimer, Annuler, Forcer | Élevée |
| **Primaires concurrentes** `primary` + `accent` pleins sur le même écran | `sampling-rounds`, `orders` | Moyenne |
| **Aucun `mat-flat-button`** — usage systématique de `raised` (ombre) y compris pour des actions secondaires | global | Faible (cohérence) |
| 4 boutons d'en-tête à poids visuel quasi égal | `sampling-rounds` détail | Moyenne |

Comptage indicatif des boutons par page : `sampling-rounds` raised=10 stroked=8 warn=4 ·
`orders` raised=7 stroked=6 warn=1 · `sampling-plans` raised=6 stroked=3 warn=4 ·
`sampling-locations` raised=5 stroked=1 warn=2.

## 3. Constats par écran

### 3.1 Tournée — détail (`sampling-round-detail.component.ts`) — PRIORITAIRE
En-tête actuel (gauche→droite) : **🗑 Supprimer** · **✕ Annuler la tournée** · **➕ Assigner**
(bleu plein) · **+ Ajouter un mandat**.
- **Problème** : les 2 destructives sont les plus à gauche (lues comme prioritaires) ;
  pas d'action primaire unique claire ; « Ajouter un mandat » sous-valorisé.
- **Reco** : barre = `[ ⋮ ]  …………  [Ajouter un mandat] [Action primaire selon statut]`
  où l'action primaire = **Démarrer** (Assigned) / **Assigner** (Draft) / **Tout transmettre**
  (InProgress), à droite, en plein. **Supprimer / Annuler / Forcer** → dans le menu **⋮**.

### 3.2 Mandats / commande (`orders`)
- **Problème** : `confirmDelete` en **bouton plein rouge** (action la plus visible de l'écran).
- **Reco** : déplacer la suppression dans un **⋮**, garder l'action primaire (Valider/Transmettre)
  en plein ; harmoniser les multiples raised primary/accent.

### 3.3 Lieux de prélèvement (`sampling-locations`)
- **Problème** : `onDelete` en **bouton plein rouge** dans le dialog/détail.
- **Reco** : suppression en stroked discret à gauche, **action primaire (Enregistrer) à droite**
  en plein ; respecter l'ordre `[Supprimer] ………… [Annuler] [Enregistrer]`.

### 3.4 Plans de prélèvement (`sampling-plans`)
- **Problème** : `deletePlan` + `rejectPlan` en stroked warn proéminents (4 warn au total).
- **Reco** : Rejeter/Supprimer dans **⋮** ; l'action primaire = Approuver/Soumettre à droite.

### 3.5 Catalogue (`analysis-catalog`), Admin délégations
- Les `mat-icon-button color="warn"` **inline** dans les tableaux (retirer profil/délégation)
  sont **acceptables** (action contextuelle de ligne), mais ajouter un `aria-label` + confirmation.

### 3.6 Écrans de liste (Tournées, Lieux, Utilisateurs, Résultats) — mobile
- **Bon** : filtres empilés, tableaux → cartes, pas de débordement horizontal (vérifié e2e).
- **À surveiller** : bouton « + Créer/Nouvelle … » pleine largeur en mobile = OK ; garder ce pattern.

### 3.7 Typographie / cohérence
- Vérifier que titres de page (« Tableau de bord », « Tournées de prélèvement »,
  « Résultats d'analyses »…) partagent la **même** taille/poids (échelle unique).
- Boutons : uniformiser la casse et l'iconographie (icône + libellé, même gabarit).

## 4. Plan d'application (après validation)

1. Définir/exposer des **classes utilitaires** ou un mini pattern (barre d'actions : zone
   gauche = retour/contexte, zone droite = secondaires + primaire ; overflow ⋮ pour destructif).
2. Appliquer écran par écran selon les priorités : **Tournée détail → Mandats → Lieux →
   Plans → reste**.
3. **Non-régression** : captures e2e avant/après (desktop + mobile) par écran + scénarios
   `@mobile` existants (overflow) qui restent verts.

## 5. Priorisation

| Priorité | Élément |
|---|---|
| P1 | Tournée détail (en-tête, destructives → ⋮, action primaire unique) |
| P1 | Suppression en rouge plein → discret (orders, sampling-locations) |
| P2 | Primaires concurrentes (primary+accent) → 1 primaire/écran |
| P2 | Plans de prélèvement (Rejeter/Supprimer → ⋮) |
| P3 | Échelle typographique unique + cohérence iconographie/casse |
| P3 | `raised` → `flat` pour actions sans besoin d'élévation (cohérence) |
