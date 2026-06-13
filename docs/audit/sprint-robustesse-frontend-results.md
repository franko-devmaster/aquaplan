# Sprint Robustesse — Frontend — Résultats

| | |
|---|---|
| **Branche** | `feature/sprint-robustesse-frontend` (depuis `Main` @ `0a054ae`) |
| **Remote** | `github` (`franko-devmaster/aquaplan`) |
| **Périmètre** | 12 findings High / visibles de l'audit Fable frontend (F-003 → F-010, F-035, F-004, F-005, F-009) |
| **Build** | `ng build --configuration production` → 0 erreur · `dotnet build AquaPlan.slnx -c Release` → 0 erreur |
| **Tests Angular** | Aucun (convention projet : pas de tests unitaires Angular) |

---

## Commits par lot

| Lot | SHA | Titre |
|---|---|---|
| K + N | `1b814ca` | fix(frontend): F-006..F-010 F-035 F-009 — data robustness, races & bell color |
| L | `a48f514` | fix(frontend): F-004 F-005 — fr-CH locale & full de/en i18n parity |
| M | `128d6f8` | perf(frontend): F-003 — lazy-load business routes |

---

## Lot K — Robustesse données / races

### F-006 — Filtres invisibles persistants (deep-links cassés) ✅
`order-list.component.ts` (`applyStatusesFromQueryParams`) et `sampling-round-list.component.ts`
(`applyQueryParams`) **réinitialisent désormais systématiquement** les filtres pilotés par query
params quand le paramètre est absent (`statusFilter`, `resultsStatusFilter`, `preleveurFilter`).
Avant : un filtre posé par une tuile dashboard (« Mes tournées », « Conformes ») restait actif
silencieusement au retour via la sidebar, sans contrôle UI visible. Approche : reset à l'init plutôt
qu'un `resetFilters()` global, pour ne pas casser les filtres pilotés par l'UI (recherche,
distributeur, dates).

### F-007 — Course de réponses HTTP ✅
Garde par numéro de séquence (`requestSeq`) ajoutée dans `loadFiltered()` des datastores
**order, sampling-round, sampling-plan, sampling-location** (4 datastores à filtrage serveur).
Une réponse n'est appliquée que si elle correspond à la dernière requête émise ; une requête lente
périmée ne peut plus écraser une réponse plus récente. Le `loading` n'est remis à false que par la
requête courante.

### F-008 — Course au callback OIDC ✅
`auth-callback.component.ts` consommait déjà le nouveau flux `?code` + `POST /api/auth/oidc-exchange`
(Sprint Sec) et **attendait** `setTokensFromOidc` avant `router.navigate`. Complété côté
`auth.service.ts` : `setTokensFromOidc` pose maintenant le signal `accessToken` **avant** toute
persistance async (IndexedDB) et réassigne `_userLoaded` synchroniquement, puis `await`. Le
`authorizeGuard` voit donc un utilisateur authentifié et chargé dès la navigation, sans rebond vers
`/login` dépendant de la latence IDB.

### F-010 — Tentatives de replay non persistées ✅
- `offline-storage.service.ts` : nouvelle méthode `updateActionAttempts(id, attempts)` (persiste le
  compteur dans le store `pending-actions`).
- `sync.service.ts` : `replayAction` reprend `attempts` depuis IDB, le **persiste après chaque échec
  5xx**, et applique un plafond cumulé `MAX_CUMULATIVE_ATTEMPTS = 10` à travers les flushes et les
  reconnexions. Une action durablement en échec est abandonnée (retirée de la file) avec un toast
  dédié `sync.permanentlyFailed` au lieu de relancer 3 tentatives complètes à chaque événement
  `online`. Le champ `attempts`, jusque-là code mort, est maintenant fonctionnel.

### F-035 — Compteurs dashboard figés après actions bulk ✅
Nouveau `DashboardRefreshService` (signal `version` + `notifyBulkChange()`). Les trois actions bulk
de `order-list.component.ts` (`bulkValidate`, `bulkFinalize`) appellent `notifyBulkChange()` après
succès. `home.component.ts` réagit via un `effect` (skip de la première émission, `ngOnInit` faisant
déjà le chargement initial) et recharge les compteurs des tuiles. Les compteurs ne restent plus figés
sur des valeurs périmées.

---

## Lot L — i18n + locale

### F-004 — Locale Angular fr-CH ✅
`app.config.ts` : `registerLocaleData(localeFrCH)` + `{ provide: LOCALE_ID, useValue: 'fr-CH' }` +
`{ provide: MAT_DATE_LOCALE, useValue: 'fr-CH' }`. Les `DatePipe` rendent désormais DD.MM.YYYY et les
nombres au format suisse ; le datepicker Material suit fr-CH. Bonus **F-026** : la cloche de
notifications utilise `| date:'dd.MM.yyyy HH:mm'` au lieu de `toLocaleString()` dépendant du
navigateur (méthode `formatDate` morte supprimée).

### F-005 — i18n de.json / en.json complétés ✅
`de.json` et `en.json` **réécrits à parité complète** avec `fr.json` (référence).

| Fichier | Avant | Après |
|---|---|---|
| fr.json | 514 clés | 515 (ref + `sync.permanentlyFailed`) |
| de.json | 360 clés (−166) | **515** |
| en.json | 367 clés (−159) | **515** |

Sections ajoutées : `sampling.*` (formulaire terrain), `results.*` (écran résultats + byRound/
byLocation), `samplingPlans.*`, `sectors.*`, l'essentiel de `samplingRounds.*`, `nav.samplingPlans/
samplingRounds/sectors`, `orders.linkToRound` et apparentés, `samplingLocations` (address,
accessDescription, sector…), `users.userNumber`, `distributors.shortName`, etc. Traductions réelles :
allemand suisse (Hochdeutsch, `ß`→`ss`), anglais — aucun placeholder, aucune clé restée en français
(vérifié par script). Les **12 clés legacy** `orders.status.draft/assigned/sentToLims/...` +
`orderStatus.descriptions.*` ont été purgées (F-025). Parité vérifiée : 0 clé manquante, 0 clé extra
dans les deux sens.

---

## Lot M — Performance bundle

### F-003 — Lazy-loading des routes ✅
`app.routes.ts` : les 18 routes métier converties en `loadComponent: () => import(...)`. Restent
eager : `LayoutComponent` (shell) et `HomeComponent` (enfant par défaut rendu au boot). `login` +
`auth/callback` aussi lazy (hors chemin critique authentifié). Les guards (`authorizeGuard`,
`featureGuard`) sont **préservés** sur chaque route lazy.

**Impact bundle (`ng build --configuration production`) :**

| | Avant | Après |
|---|---|---|
| `main.js` (raw) | 1.30 MB | **93.27 kB** |
| `main.js` (transfer) | 217.78 kB | **20.08 kB** |
| Initial total (raw) | 1.66 MB | **929 kB** |
| Initial total (transfer) | 291.30 kB | **213.82 kB** |
| Lazy chunks | 2 (zxing) | **~45** (un par page métier) |

Le code admin/catalogue/plans/résultats n'est plus chargé au démarrage : un préleveur terrain sur
4G ne télécharge que le shell + dashboard au boot.

> Note : le `Initial total` reste à ~929 kB car il inclut le CSS (azure-blue Material + tokens,
> 160 kB — cf. F-023, hors périmètre de ce sprint) et un chunk vendor partagé. Le gain réel sur le
> JS applicatif évalué au boot est majeur (main 1.30 MB → 93 kB).

---

## Lot N — UX

### F-009 — Couleur de la cloche de notifications ✅
`notifications-bell.component.ts` : `.notifications-trigger { color: var(--color-fg-default); }`
(était `#fff`, hérité du header bleu pré-AQ-423). `.urgent-bell` passe de `#FFCDD2` à
`var(--color-error-500)`. L'icône est désormais visible sur le header blanc AQ-423.

---

## Findings traités vs reportés

**Traités (12 du périmètre) :** F-003, F-004, F-005, F-006, F-007, F-008, F-009, F-010, F-035.
**Bonus traités au passage :** F-025 (clés legacy purgées), F-026 (date cloche via DatePipe).

**Déjà partiellement corrigé dans Main (noté, non refait) :**
- **F-001 / F-008 (volet callback)** : le flux OIDC en query string (F-001) a été remplacé par
  `?code` + `POST /api/auth/oidc-exchange` lors du Sprint Sec ; `auth-callback.component.ts` attendait
  déjà la pose du token avant navigation. Seul l'ordre signal-avant-persistance dans
  `setTokensFromOidc` restait à durcir (fait).

**Hors périmètre (reportés au backlog audit) :** F-011→F-038 (Moyenne) et F-039→F-056 (Basse) —
notamment F-023 (Bootstrap/thème mort dans le bundle, qui explique le poids CSS résiduel), F-022
(client NSwag mort), F-039/040 (tokens hex restants).

---

## Problèmes rencontrés

- Aucun blocage. La branche backend `feature/sprint-robustesse-backend` (PR #2) n'a pas été utilisée
  comme base : départ strict depuis `Main` (`0a054ae`) comme demandé.
- Le warning de budget de style sur `home.component.ts` (4.73 kB > 4 kB) est **pré-existant** (présent
  sur le build de référence avant toute modification) — non régressé par ce sprint.
- `dotnet build -c Release` : 0 erreur (112 warnings pré-existants, aucun introduit — frontend
  uniquement modifié).
