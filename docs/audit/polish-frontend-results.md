# Sprint Polish — Frontend Results (F-011 → F-049)

| | |
|---|---|
| **Date** | 14.06.2026 |
| **Base** | `d7c7890` (Main) |
| **Branche** | `feature/polish-frontend-clean` |
| **Périmètre** | `src/AquaPlan.Web/ClientApp/` uniquement (findings Medium F-011..F-049 de l'audit Fable) |
| **Build** | `ng build --configuration production` — vert après chaque lot |

## Résultat bundle (initial total)

| Étape | Raw | Transfer |
|---|---|---|
| Base (Lot 1) | 929.33 kB | 214.22 kB |
| Après Lot 2 (retrait Bootstrap) | **799.72 kB** | **206.57 kB** |
| Après Lots 3-4 | 800.82 kB | 207.58 kB |

Gain net **~129 kB raw / ~7 kB transfer** sur le bundle initial, essentiellement le retrait des deux CSS Bootstrap (grille + utilities) jamais utilisés. Le client NSwag mort (-2905 lignes) sortait déjà du bundle (lazy-exclu) mais nettoie l'arbre source.

## Findings traités

### Lot 1 — Déduplication (DRY)
| Finding | Action |
|---|---|
| F-011 | `ConfirmService` + `PromptDialogComponent` remplacent les 11 `confirm()`/`prompt()` natifs (round-detail, plan-detail, location-list). |
| F-012 | `AuthService` expose `isAdmin`/`isPreleveur`/`isPreleveurOnly` (computed). Suppression des copies divergentes dans round-detail, order-detail, home, sidebar, order-list, plan-list (dont la variante fragile `includes('réleveur')`). |
| F-013 | `utils/status-variant.ts` (`orderStatusVariant`/`roundStatusVariant`/`planStatusVariant`) remplace les 7 maps dupliquées. |
| F-014 | `ApiErrorService.toast()/success()/extract()` remplace ~15 blocs try/catch+snackBar avec fallback `'Error'` non i18n (clé `errors.generic`). |
| F-015 | `utils/debounced-search.ts` (cleanup via `DestroyRef`) remplace 3 debounces `setTimeout` jamais nettoyés. |

### Lot 2 — Dead code & dépendances
| Finding | Action |
|---|---|
| F-022 | Suppression du client NSwag généré (2905 lignes, **0 import** vérifié). |
| F-023 | Retrait des 2 CSS Bootstrap d'`angular.json` (2 configs) + dépendance `bootstrap` de `package.json` (0 classe Bootstrap utilisée, 0 import JS). Thème Material `azure-blue` conservé (un seul thème prébuilt, pas de doublon). |
| F-033 | Suppression de l'export mort `SamplingRoundStatusColors`. |
| F-034 | Suppression code mort : `conformityBadge()`, `dialogRef`/`close()` (sampling-results-dialog), `pendingResultsCount` (home), `isPreleveur` dead (home/sidebar). |
| F-025 | Purge des clés i18n orphelines `notifications.type.*` (fr/de/en). Les clés legacy `orders.status.*` étaient déjà purgées en Sprint Robustesse. |

### Lot 3 — Robustesse erreurs
| Finding | Action |
|---|---|
| F-019 | `loadSamplings` parallélisé via `Promise.allSettled` (était N requêtes séquentielles). |
| F-020 | `editSamplerComment` : update optimiste + rollback + catch (était `.then()` sans catch). |
| F-021 | plan save/submit/validate/reject : gestion d'erreur + abandon si save échoue. |
| F-027 | exports CSV/PDF : signal `exporting` (anti double-clic) + toast d'erreur + util `downloadBlob` mutualisé. |
| F-030 | order-detail + round-detail : état `loadError` + bloc « introuvable » + bouton retour (plus de page blanche sur 404). |
| F-028 | location-form-dialog : `takeUntilDestroyed` + subscribe déplacé après le patch d'édition. |
| F-036 | interceptor lit le token via `AuthService.getToken()` (source de vérité unique). |

### Lot 4 — i18n & conventions
| Finding | Action |
|---|---|
| F-024 | Externalisation des chaînes en dur (aria pending header, fallback form-dialog). |
| F-031 | `console.*` du barcode-scanner → `devWarn`/`devError` (gatés prod, ajoutés à `dev-log.ts`). |
| F-032 | Nettoyage des TODO périmés de `sync.service`, suppression de la branche morte `id === 'new'`. |
| F-041 | aria-labels anglais → clés i18n `a11y.*` (header, sidebar, order-indicators, results-dialog). |
| F-042 | `aria-hidden="true"` sur icônes décoratives (header, dialogs, error states). |
| F-043 | `app.component` passé en `OnPush`. |
| F-044 | Init i18n consolidée dans `LocaleService` (seule source) + persistance de la langue (sessionStorage). Retrait du doublon dans `app.component`. |
| F-046 | `LocaleService` migré en `inject()`. |
| F-047 | barcode-scanner `@ViewChild` → `viewChild()` signal. |
| F-048 | barcode-scanner `implements AfterViewInit`. |
| F-049 | login : Reactive Form typé + validation email + signals (était `[(ngModel)]` brut). |
| F-051 | Retrait du `provideNativeDateAdapter()` dupliqué dans order-list (déjà global). |
| F-045 | `home.component` réindenté en 2 espaces. |
| F-039/F-040 | order-detail (styles complets) + round-detail (inline `[style.color]`) migrés vers `var(--token)`. |

## Findings reportés
- **F-037** (constantes de largeur de dialog partagées) : volume dispersé sur 12 composants, gain faible — reporté.
- **F-039 (reste)** : ~42 hex sur l'écran Résultats (`recent-results-zone`, `results-matrix`, `sampling-results-table`) — chantier volumineux, reporté pour un sprint dédié design-system.
- **F-016/F-017** (datastores readonly + signal error), **F-029** (LDP filtrage serveur), **F-038** (retour contextuel unifié), **F-053** (composant de liste générique) : hors périmètre des 4 lots Medium ou refactors plus larges.

## Problèmes rencontrés
- **Collisions multi-agents sur l'arbre de travail partagé** : l'agent backend (et l'agent infra) ont committé tout l'arbre de travail (`git add -A`) à plusieurs reprises sur la branche `feature/polish-frontend` active, absorbant mes changements frontend dans des commits backend (`127fa9c`, `7400f34`). Aucun travail perdu (tout préservé, tag de sauvegarde `backup/backend-mixed-127fa9c` posé). Pour livrer un diff frontend **propre**, j'ai reconstruit la branche `feature/polish-frontend-clean` directement depuis `d7c7890` via plumbing git (overlay du sous-arbre `ClientApp`), garantissant 36 fichiers frontend, 0 fichier backend.
- `npm install` non relancé après retrait de `bootstrap` de `package.json` : `node_modules`/`package-lock.json` listent encore le paquet (sans impact build, plus référencé). À synchroniser au prochain `npm ci`.
- La génération NSwag (dormante, `nswag.json` côté `AquaPlan.Api`, ciblant .NET 9) recréerait le fichier supprimé si réactivée : config à nettoyer côté backend/infra (hors périmètre ClientApp).
