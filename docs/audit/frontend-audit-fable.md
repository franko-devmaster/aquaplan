# Audit Frontend AquaPlan — Angular 19 PWA

| | |
|---|---|
| **Date** | 11.06.2026 |
| **Commit audité** | `a863679` (branche `Main`) |
| **Périmètre** | `src/AquaPlan.Web/ClientApp/src/app/**` (pages, components, services, datastore, handlers, guards, models), `src/styles.scss`, `src/styles/*.scss`, i18n `assets/i18n/*.json`, `angular.json`, `ngsw-config.json` |
| **Méthode** | Lecture exhaustive des 48 composants, 24 services, 12 datastores, 16 modèles, guards/interceptor ; analyse croisée i18n (514 clés fr) ; greps systématiques (XSS, storage, `any`, OnPush, `*ngIf`, subscribe, hex colors, TODO) |
| **Auditeur** | Senior Frontend Architect (Claude) |

> Note de périmètre : les dossiers `pipes/`, `directives/`, `utils/`, `types/`, `constants/` mentionnés dans CLAUDE.md n'existent pas dans le code actuel — aucun pipe custom n'est défini. Le fichier `api-services.service.generated.ts` (NSwag) est exclu de l'audit de style mais traité au titre du code mort (F-022).

---

## 1. Synthèse par sévérité

| Sévérité | Nombre | Findings |
|---|---|---|
| **Critique** | 2 | F-001, F-002 |
| **Haute** | 8 | F-003 → F-010 |
| **Moyenne** | 28 | F-011 → F-038 |
| **Basse** | 18 | F-039 → F-056 |
| **Total** | **56** | |

### Points positifs (vérifiés, non-findings)

- **Aucun XSS exposé** : zéro `innerHTML`, zéro `bypassSecurityTrust*`, zéro `DomSanitizer` dans tout le code applicatif.
- **Aucun `localStorage`** : le JWT est persisté en sessionStorage + IndexedDB conformément à AQ-409 (`auth.service.ts`, `offline-storage.service.ts`).
- **Aucun `any`** hors fichier généré ; zéro `@Input()/@Output()` décorateurs (tout en `input()/output()` signals).
- **Zéro `*ngIf`/`*ngFor`** : 100 % des templates en `@if`/`@for`/`@switch`.
- **47/48 composants en `ChangeDetectionStrategy.OnPush`** (seule exception : `app.component.ts`, F-043).
- **`inject()` partout** sauf une exception (F-046).
- L'architecture offline (snapshot IndexedDB + queue FIFO + replay avec backoff, AQ-375/376/409/427) est globalement bien conçue et bien typée.

---

## 2. Findings — Critique

### F-001 — Tokens OIDC (access + refresh) transmis en query string ★ Critique

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/pages/auth-callback/auth-callback.component.ts:25-35`

```ts
ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const token = params.get('token');
    const refresh = params.get('refresh');

    if (token && refresh) {
      void this.authService.setTokensFromOidc(token, refresh);
      this.router.navigate(['/']);
```

**Raison** : le flux OIDC redirige vers `/auth/callback?token=<JWT>&refresh=<refresh_token>`. Les deux tokens transitent en clair dans l'URL : ils sont écrits dans l'historique du navigateur, les logs nginx/proxy, potentiellement l'en-tête `Referer`, et restent visibles dans la barre d'adresse. Le refresh token est un secret longue durée — c'est la pire donnée à exposer ainsi. Application gouvernementale : surface inacceptable.

**Proposition** : passer à un échange par `authorization code` côté backend (cookie HttpOnly de session courte, ou fragment `#` + `history.replaceState` au strict minimum). À défaut, POST du code vers `/api/auth/oidc-exchange` et nettoyage immédiat de l'URL (`location.replaceState`). Coordonner avec le backend (endpoint `/api/auth/oidc-login`).

**Effort** : M (backend + frontend)

---

### F-002 — Saisie de prélèvement offline : validation jamais vérifiée → perte de données terrain ★ Critique

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/pages/sampling-rounds/sampling-form-dialog.component.ts:302-401`

```ts
async save(): Promise<void> {
    this.errorMessage.set(null);
    // ... contrôle des codes-barres uniquement ...
    this.saving.set(true);
    try {
      const formValue = this.form.getRawValue();   // <-- this.form.invalid jamais testé
      // ...
      if (!this.syncService.onlineStatus()) {
        await this.offlineStorage.queueAction({ roundId: roundKey, actionType, payload: { orderId: ..., dto } });
        // ...
        this.dialogRef.close(optimistic);          // <-- dialog fermé, données considérées sauvées
```

**Raison** : `temperature` et `weather` portent `Validators.required` (lignes 214-215) mais `save()` ne teste jamais `this.form.invalid`. En ligne, le backend renvoie 400 et l'erreur s'affiche. **Hors ligne**, le payload invalide est mis en queue, le dialog se ferme avec un DTO optimiste (le préleveur croit la saisie enregistrée), puis au retour réseau le replay reçoit un 4xx → `sync.service.ts:179-191` **supprime l'action de la queue** avec un simple toast `sync.rejected`. La saisie terrain est définitivement perdue, des heures après coup.

**Proposition** : ajouter `if (this.form.invalid) { this.form.markAllAsTouched(); return; }` en tête de `save()`. En complément : côté `SyncService`, sur rejet 4xx d'un `CREATE_SAMPLING`/`UPDATE_SAMPLING`, conserver le payload dans un store « actions rejetées » consultable au lieu de le jeter.

**Effort** : XS (garde-fou) + S (store des rejets)

---

## 3. Findings — Haute

### F-003 — Aucune route lazy-loadée : tout le métier dans le bundle initial

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/app.routes.ts:1-55`

```ts
import { LoginComponent } from './pages/login/login.component';
import { HomeComponent } from './pages/home/home.component';
import { OrderDetailComponent } from './pages/orders/order-detail.component';
// ... 20 imports statiques ...
export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  ...
  { path: 'admin/users', component: UserListComponent, canActivate: [featureGuard(['Administrator'])] },
```

**Raison** : les 21 routes référencent des composants importés statiquement — aucune n'utilise `loadComponent`/`loadChildren`. Tout le code (admin, catalogue, plans, résultats…) est chargé au boot, y compris pour un préleveur sur iPhone en 4G qui n'utilisera que tournées + saisie. Le budget `initial` est d'ailleurs déjà calibré large (2 MB warning / 3 MB error dans `angular.json`). Seul `@zxing/browser` est en import dynamique.

**Proposition** : convertir au minimum les zones `admin/**`, `analysis-*`, `sampling-plans`, `results` en `loadComponent: () => import(...)`. Gain immédiat sur le TTI mobile terrain.

**Effort** : S

---

### F-004 — Locale Angular non configurée : dates affichées au format US, pas DD.MM.YYYY

**Fichiers** : `src/AquaPlan.Web/ClientApp/src/app/app.config.ts` (absence), usages multiples

Constat vérifié : **aucun** `registerLocaleData`, aucun provider `LOCALE_ID`, aucun `MAT_DATE_LOCALE` dans tout `src/`. Or 13 usages du `DatePipe` reposent sur des formats locale-dépendants :

```
7× | date:'shortDate'   → rend « 6/11/26 » (en-US) au lieu de « 11.06.2026 »
3× | date:'short'       → « 6/11/26, 3:04 PM »
2× | date:'medium'  1× | date:'mediumDate'
3× seulement en 'dd.MM.yyyy' explicite (home, recent-results-zone, results-matrix)
```

Exemple `pages/orders/order-list.component.ts:175` :
```html
{{ order.plannedDate ? (order.plannedDate | date:'shortDate') : '-' }}
```

Le `provideNativeDateAdapter()` (app.config.ts:41) sans `MAT_DATE_LOCALE` rend aussi les datepickers en MM/DD/YYYY.

**Proposition** : `registerLocaleData(localeFrCH)` + `{ provide: LOCALE_ID, useValue: 'fr-CH' }` + `{ provide: MAT_DATE_LOCALE, useValue: 'fr-CH' }` dans `app.config.ts`, puis uniformiser sur les formats courts standards (qui rendront alors dd.MM.yyyy nativement).

**Effort** : XS (providers) + S (vérification visuelle des 13 usages)

---

### F-005 — i18n : 166 clés manquantes en allemand, 159 en anglais (≈ 32 % de l'UI)

**Fichiers** : `src/AquaPlan.Web/ClientApp/src/assets/i18n/{fr,de,en}.json`

Mesure exacte (clés aplaties) : **fr = 514 clés, de = 360, en = 367**. Manquent notamment en DE : tout `sampling.*` (le formulaire de prélèvement complet), `results.*` (l'écran résultats), `orders.linkToRound`, `nav.samplingPlans/samplingRounds/sectors`, `orderStatus.descriptions.*`… Avec `defaultLanguage: 'fr'`, un utilisateur DE voit une interface mélangée allemand/français. Le canton de Fribourg est officiellement bilingue.

**Proposition** : compléter `de.json` en priorité (la liste exacte des 166 clés est reproductible via diff des clés aplaties) ; ajouter un check CI qui échoue si les jeux de clés divergent.

**Effort** : M (traduction) + XS (script CI)

---

### F-006 — Filtres invisibles persistants dans les datastores singletons (deep-links)

**Fichiers** : `src/AquaPlan.Web/ClientApp/src/app/pages/sampling-rounds/sampling-round-list.component.ts:190-219`, `pages/orders/order-list.component.ts:463-485`, `datastore/sampling-round.datastore.ts`, `datastore/order.datastore.ts`

```ts
// sampling-round-list.component.ts — applyQueryParams()
const preleveurParam = params.get('preleveurId');
if (preleveurParam) {
  ...
  this.store.preleveurFilter.set(resolved);
}
// ← aucun else : le filtre précédent n'est JAMAIS remis à zéro
```

**Raison** : les datastores sont `providedIn: 'root'` ; leurs filtres survivent à la navigation. Scénario réel : l'utilisateur clique « Mes tournées » sur le dashboard (AQ-411, pose `preleveurFilter`), puis revient plus tard sur `/sampling-rounds` via la sidebar **sans** query params → `applyQueryParams` ne réinitialise rien → la liste reste silencieusement filtrée sur son propre id, **sans aucun contrôle UI visible** pour ce filtre. Identique pour `resultsStatusFilter` côté mandats (tuiles « Conformes / Non conformes » du dashboard) : aucun contrôle UI ne matérialise ce filtre dans la liste.

**Proposition** : dans `applyQueryParams()`, réinitialiser systématiquement les filtres pilotés par query params quand le paramètre est absent (`else this.store.preleveurFilter.set(undefined)`), ou exposer une méthode `store.resetFilters()` appelée en `ngOnInit`.

**Effort** : S

---

### F-007 — Course de réponses HTTP : résultats périmés peuvent écraser les récents

**Fichiers** : `src/AquaPlan.Web/ClientApp/src/app/datastore/order.datastore.ts:40-63`, `datastore/sampling-round.datastore.ts:34-53` (même patron dans tous les datastores de liste)

```ts
async loadFiltered(): Promise<void> {
    this.loading.set(true);
    try {
      const filter: OrderFilterDto = { ... };
      const result = await firstValueFrom(this.api.getFiltered(filter));
      this.orders.set(result.items);          // <-- aucune protection out-of-order
```

**Raison** : la recherche est debouncée à 300 ms mais chaque frappe/filtre déclenche un `loadFiltered()` concurrent. Deux requêtes en vol → si la première (large) répond après la seconde (filtrée), la liste affiche les résultats périmés avec les filtres récents. Pattern `switchMap` absent puisque tout est en promesses.

**Proposition** : numéro de séquence (`const reqId = ++this.lastReqId; ... if (reqId !== this.lastReqId) return;`) ou refactor vers un flux RxJS `switchMap` alimenté par les signals de filtres.

**Effort** : S (par datastore, 6 concernés)

---

### F-008 — Course au callback OIDC : navigation avant la pose du token

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/pages/auth-callback/auth-callback.component.ts:30-33` + `services/auth.service.ts:175-179`

```ts
if (token && refresh) {
  void this.authService.setTokensFromOidc(token, refresh);  // async non attendu
  this.router.navigate(['/']);                              // part immédiatement
}
```

`setTokensFromOidc` est `async` : il attend `persistTokens()` (écriture IndexedDB) **avant** de poser `accessToken.set(...)`. La navigation démarre donc pendant que le token n'est pas encore dans le signal ; `authorizeGuard` attend `whenUserLoaded()` — qui référence encore l'ancienne promesse résolue si la réassignation de `_userLoaded` n'a pas eu lieu — puis teste `isAuthenticated()` → risque de rebond vers `/login` après un SSO réussi (dépend de la latence IndexedDB).

**Proposition** : `await this.authService.setTokensFromOidc(...)` puis `await this.router.navigate(['/'])` (ngOnInit peut être async). Dans `setTokensFromOidc`, poser le signal avant les écritures de persistance.

**Effort** : XS

---

### F-009 — Cloche de notifications : icône blanche sur header blanc (régression AQ-423)

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/components/notifications-bell/notifications-bell.component.ts:83-86`

```scss
.notifications-trigger {
  color: #fff;          /* hérité du header bleu primaire pré-AQ-423 */
  margin-right: 4px;
}
```

**Raison** : AQ-423 a refondu le header en surface blanche (`header.component.ts` : `background: var(--color-bg-surface)`), mais la cloche a conservé `color: #fff` daté du header bleu. Aucun override global trouvé (`grep notifications-trigger` dans `styles.scss`/`styles/` : zéro résultat). L'icône est très probablement invisible ou quasi invisible. À confirmer visuellement, mais le code est incohérent dans tous les cas.

**Proposition** : `color: var(--color-fg-default)` (ou `--color-fg-muted`) + badge warn inchangé ; vérifier `urgent-bell` (#FFCDD2, pastel clair) dans la foulée.

**Effort** : XS

---

### F-010 — Tentatives de replay non persistées : budget de retry réinitialisé à chaque flush

**Fichier** : `src/AquaPlan.Web/ClientApp/src/app/services/sync.service.ts:152-210` + `services/offline-storage.service.ts:33-40`

```ts
private async replayAction(action: PendingAction): Promise<...> {
    let attempt = action.attempts;          // lu depuis IDB...
    while (attempt < MAX_RETRIES) {
      ...
      attempt++;                            // ...mais jamais réécrit dans IDB
```

**Raison** : `PendingAction.attempts` est initialisé à 0 (`queueAction`) et lu au replay, mais **jamais persisté** après échec. Chaque événement `online` relance donc 3 tentatives complètes (2 s + 5 s + 15 s de `sleep` bloquant la file) pour une action durablement en échec 5xx — indéfiniment, sans plafond global ni visibilité. Le champ `attempts` est de fait du code mort.

**Proposition** : après chaque échec 5xx, `db.put('pending-actions', { ...action, attempts })`; au-delà d'un plafond cumulé (p.ex. 10), déplacer l'action dans un état « en erreur » visible dans l'UI (badge header existant).

**Effort** : S

---

## 4. Findings — Moyenne

### F-011 — `confirm()` / `prompt()` natifs : 13 occurrences alors que `ConfirmDialogComponent` existe

**Fichiers** : `pages/sampling-rounds/sampling-round-detail.component.ts:641,697,752,766,830,869,896,1007,1062` ; `pages/sampling-plans/sampling-plan-detail.component.ts:410,426,453` ; `pages/sampling-locations/sampling-location-list.component.ts:217`

```ts
if (!confirm(this.translate.instant('samplingRounds.confirmDelete'))) return;
// ...
const newComment = prompt(this.translate.instant('samplingRounds.editComment'), order.samplerComment ?? '');
```

**Raison** : deux patterns coexistent — `ConfirmDialogComponent` (Material, utilisé dans orders/users/delegations) et les dialogs natifs bloquants (non stylés, non thémés, rendu inégal sur iOS PWA standalone, intitulés boutons non localisables). Le `prompt()` pour le motif de rejet d'un plan (`sampling-plan-detail.ts:410`) limite la saisie à une ligne sans validation.

**Proposition** : remplacer tous les `confirm()` par `ConfirmDialogComponent` ; créer un petit `PromptDialogComponent` pour les deux `prompt()`.

**Effort** : M

### F-012 — Détection du rôle Préleveur : 3 implémentations divergentes copiées dans 4+ composants

**Fichiers** : `pages/sampling-rounds/sampling-round-detail.component.ts:454-461`, `pages/home/home.component.ts:266-272`, `pages/orders/order-detail.component.ts:223-227`, `components/sidebar/sidebar.component.ts:270-284`

```ts
// round-detail : regex tolérante aux accents cassés
return user.roles.some(r => /pr[eéè]leveur/i.test(r));
// home / order-detail / sidebar : substring fragile
return user.roles.some(r => r.toLowerCase().includes('réleveur')) && !user.roles.includes('Administrator');
// sidebar (isPreleveurOnly) : égalité stricte
return roles.some(r => r === 'Préleveur');
```

**Raison** : trois sémantiques différentes pour la même question métier. La variante `includes('réleveur')` matche aussi `Requérant-Préleveur` (voulu ? non documenté) ; la variante stricte non. Risque réel d'incohérences de droits UI entre écrans (boutons visibles ici, masqués là). `isAdmin` est lui dupliqué dans 8 composants.

**Proposition** : centraliser dans `AuthService` des `computed` uniques (`isAdmin`, `isPreleveur`, `isPreleveurOnly`, `isRequerant`) — `canCreateOrders` montre déjà la voie — et supprimer toutes les copies locales.

**Effort** : S

### F-013 — Mapping statut → variante de chip dupliqué dans 7 composants

**Fichiers** : `home.component.ts:312-321`, `order-list.component.ts:513-523`, `order-detail.component.ts:255-267`, `sampling-round-list.component.ts:225-234`, `sampling-round-detail.component.ts:526-547`, `sampling-plan-list.component.ts`, `sampling-plan-detail.component.ts`

```ts
const map: Record<string, StatusChipVariant> = {
  'New': 'draft', 'InProgress': 'info', 'Completed': 'success',
  'Transmitted': 'success', 'Done': 'success', 'Cancelled': 'danger',
};
```

**Raison** : la même table est recopiée (avec de légères variations) sept fois. Tout changement de sémantique de statut (p.ex. distinguer `Transmitted` de `Done`) exige sept modifications synchrones. Les variantes métier dédiées du `StatusChipComponent` (`to-plan`, `sampled`, `in-analysis`… AQ-423) ne sont d'ailleurs jamais utilisées.

**Proposition** : exporter `orderStatusVariant(status)` et `roundStatusVariant(status)` depuis les modèles (`order.model.ts` / `sampling-round.model.ts`, à côté des `*StatusLabels` qui existent déjà).

**Effort** : S

### F-014 — Pattern try/catch + snackBar dupliqué ~15× (8× dans le seul round-detail)

**Fichier type** : `pages/sampling-rounds/sampling-round-detail.component.ts:736-745` (idem 777-786, 813-822, 841-850, 880-889, 967-975, 1022-1029, 1076-1083)

```ts
} catch (err: unknown) {
  const apiError = err as { error?: { error?: string } };
  this.snackBar.open(
    apiError?.error?.error ?? 'Error',
    this.translate.instant('common.close'),
    { duration: 5000 }
  );
}
```

**Raison** : duplication massive (règle de trois largement dépassée), fallback `'Error'` non i18n, et le code d'erreur API brut (`round.locked` etc.) est montré tel quel à l'utilisateur au lieu d'un libellé traduit.

**Proposition** : service `ApiErrorService.toast(err)` qui mappe les codes d'erreur backend vers des clés i18n (`errors.<code>`), avec fallback `common.error`.

**Effort** : S

### F-015 — Debounce de recherche artisanal dupliqué 3×, jamais nettoyé au destroy

**Fichiers** : `pages/orders/order-list.component.ts:331,525-533`, `pages/sampling-rounds/sampling-round-list.component.ts:171,236-244`, `pages/sampling-plans/sampling-plan-list.component.ts`

```ts
private searchTimeout: ReturnType<typeof setTimeout> | null = null;
onSearchChange(value: string): void {
  ...
  this.searchTimeout = setTimeout(() => { this.store.setSearch(value); }, 300);
}
```

**Raison** : triplication du même mécanisme ; le timer n'est pas annulé en destroy → si l'utilisateur tape puis navigue dans les 300 ms, `setSearch()` déclenche un `loadFiltered()` fantôme sur le datastore singleton (requête HTTP inutile + état filtre modifié hors écran).

**Proposition** : utilitaire commun (signal + `effect` avec `debounce`, ou petite directive `appDebouncedInput`), annulation via `DestroyRef.onDestroy()`.

**Effort** : S

### F-016 — Datastores : signals d'état exposés en écriture à tous les composants

**Fichiers** : `datastore/order.datastore.ts:20-36`, `datastore/sampling-round.datastore.ts:18-30` (pattern général)

```ts
readonly orders = signal<OrderListDto[]>([]);       // public, writable
readonly sortBy = signal<string | undefined>(undefined);
// ...côté composant :
this.store.sortBy.set(sort.active);                  // mutation directe + loadFiltered() manuel
this.store.currentPage.set(1);
```

**Raison** : `readonly` n'empêche que la réassignation du champ, pas `.set()`. Les composants mutent directement l'état interne (order-list mute 6 signals différents du store) et doivent penser à appeler `loadFiltered()` eux-mêmes — la moitié des setters encapsulés du store (`setSort`, `setPage`) ne sont du coup pas utilisés. Incohérence d'API et couplage fort.

**Proposition** : exposer `xxx.asReadonly()` + méthodes mutatrices uniques (`setSort()`, `setPage()`…) qui rechargent ; supprimer les mutations directes côté composants.

**Effort** : M

### F-017 — `loadFiltered()` fire-and-forget sans catch : rejets de promesse non gérés

**Fichiers** : `datastore/order.datastore.ts:88-125` (tous les setters), `pages/orders/order-list.component.ts:349`, `pages/sampling-rounds/sampling-round-list.component.ts:187`

```ts
setSearch(search: string): void {
    this.searchFilter.set(search);
    this.currentPage.set(1);
    this.loadFiltered();          // Promise non awaitée, aucun .catch
}
```

**Raison** : en cas d'erreur API (500, réseau), la promesse rejette sans handler → `unhandledrejection` silencieux. L'utilisateur voit une liste vide ou figée sans aucun message (le `finally` remet bien `loading` à false, mais aucun état d'erreur n'existe dans les datastores).

**Proposition** : `try/catch` dans `loadFiltered()` avec signal `error` exposé + bandeau d'erreur dans les listes.

**Effort** : S

### F-018 — Matrice résultats : recherche linéaire par cellule à chaque cycle de change detection

**Fichier** : `pages/results/results-matrix.component.ts:113-125, 280-284`

```ts
@for (date of matrix().dates; track date) {
  @let cell = cellFor(loc.id, date);          // appelé locations × dates fois
...
cellFor(locationId: string, date: string): ResultsCellDto | undefined {
    return this.matrix().cells.find(c => c.locationId === locationId && c.date === date);
}
```

**Raison** : complexité O(locations × dates × cells) ré-exécutée à chaque CD (même OnPush, tout événement dans le composant redessine la matrice). Avec 100 LDP × 30 dates × 1000 cellules = 3 M de comparaisons par cycle.

**Proposition** : `computed()` construisant une `Map<string, ResultsCellDto>` clé `${locationId}|${date}` ; `cellFor` devient un `get` O(1).

**Effort** : XS

### F-019 — `loadSamplings` : N requêtes HTTP séquentielles

**Fichier** : `pages/sampling-rounds/sampling-round-detail.component.ts:1032-1046`

```ts
for (const order of inProgressOrders) {
  try {
    const sampling = await firstValueFrom(this.samplingApi.getByOrderId(order.id));
    samplings[order.id] = sampling;
  } catch { ... }
}
```

**Raison** : une tournée de 20 mandats InProgress = 20 requêtes en série au chargement de la page ; sur réseau mobile terrain (300 ms RTT), ~6 s de latence évitable.

**Proposition** : `await Promise.allSettled(inProgressOrders.map(o => ...))` ; idéalement un endpoint batch `GET /api/sampling-rounds/{id}/samplings`.

**Effort** : XS (frontend) / S (avec endpoint batch)

### F-020 — `editSamplerComment` : `.then()` sans `.catch`, update optimiste sans rollback

**Fichier** : `pages/sampling-rounds/sampling-round-detail.component.ts:695-713`

```ts
firstValueFrom(this.roundApi.updateSamplerComment(order.id, { comment: newComment }))
  .then(() => {
    ...
    this.round.set({ ...r, orders });
  });                                   // <-- aucun catch
```

**Raison** : si l'API échoue (offline, 409), le rejet est non géré (silencieux) et l'utilisateur croit son commentaire sauvé alors que l'UI ne reflète même pas la modification (l'update est dans le `then`). Incohérent avec le reste du fichier (async/await + toast).

**Proposition** : convertir en `async/await` + `try/catch` + toast d'erreur, comme les autres actions du composant.

**Effort** : XS

### F-021 — Création de mandat : échec silencieux et séquence non atomique create → addOrder

**Fichier** : `pages/orders/order-create-dialog.component.ts:298-333`

```ts
try {
  const order = await this.orderStore.create({ ... });
  if (val.roundMode === 'existing' && val.roundId) {
    await firstValueFrom(this.roundApi.addOrder(val.roundId, order.id));
  } else if (val.roundMode === 'new' && val.newRoundName) {
    const newRound = await firstValueFrom(this.roundApi.create({ ... }));
    await firstValueFrom(this.roundApi.addOrder(newRound.id, order.id));
  }
  this.dialogRef.close(true);
} finally {
  this.saving.set(false);             // <-- aucun catch : erreur invisible
}
```

**Raison** : (1) aucune gestion d'erreur — un 4xx/5xx laisse le dialog ouvert sans message ; (2) si `create` réussit mais `addOrder` échoue, un mandat orphelin existe déjà ; l'utilisateur re-soumet → doublon. Même pattern sans catch dans `order-edit-dialog.ts:200-212`, `user-form-dialog.ts:163-192`, `sampling-plan-detail.ts:354-404` (save/submit/validate), `delegation-list.ts` (create/delete).

**Proposition** : catch + message (réutiliser `extractErrorMessage` de sampling-form-dialog) ; pour l'atomicité, endpoint backend `POST /api/orders` acceptant `roundId`/`newRound` dans le DTO.

**Effort** : S (frontend) / M (atomicité backend)

### F-022 — Client NSwag généré (2 904 lignes) : code mort, DTOs dupliqués à la main

**Fichier** : `services/api-services.service.generated.ts` (2 904 lignes) — **zéro import dans l'application** (vérifié par grep).

**Raison** : CLAUDE.md décrit le pattern « NSwag auto-génère les clients TS » mais l'app utilise 23 services API écrits à la main (`order-api.service.ts`…) avec des DTOs redéfinis manuellement dans `models/*.ts`. Double source de vérité : toute évolution d'un DTO backend peut diverger silencieusement des interfaces TS maintenues à la main (aucune erreur de compilation). Le fichier généré n'apporte que du bruit (diffs de build).

**Proposition** : trancher — soit basculer les services à la main vers le client généré (idéal : contrat garanti), soit supprimer la génération et le fichier. Documenter la décision dans CLAUDE.md.

**Effort** : L (bascule) / XS (suppression)

### F-023 — Bootstrap chargé globalement sans aucune classe utilisée + double thème Material

**Fichier** : `angular.json` (build options)

```json
"styles": [
  "@angular/material/prebuilt-themes/azure-blue.css",
  "node_modules/bootstrap/dist/css/bootstrap-grid.min.css",
  "node_modules/bootstrap/dist/css/bootstrap-utilities.min.css",
  "src/styles.scss"
]
```

**Raison** : grep exhaustif des templates : **aucune** classe Bootstrap (`row/col-*/d-flex/btn…`) n'est utilisée — AQ-412 a même renommé des classes pour *éviter* les collisions avec ce CSS mort. Par ailleurs le thème prébuilt `azure-blue` est chargé puis massivement écrasé par les tokens AQ-423 (`styles.scss` réécrit boutons, cartes, form-fields) : double poids CSS dans le bundle initial, pour une PWA terrain.

**Proposition** : retirer les deux CSS Bootstrap (et la dépendance `bootstrap` de package.json si confirmé) ; à terme remplacer `azure-blue.css` par un thème M3 minimal généré depuis les tokens.

**Effort** : XS (bootstrap) / M (thème)

### F-024 — Chaînes hard-codées non i18n (FR ou EN) dans le code

**Occurrences vérifiées** :

| Fichier | Chaîne |
|---|---|
| `sampling-form-dialog.component.ts:423` | `'Une erreur est survenue lors de l'enregistrement.'` |
| `header.component.ts:262` | `` `${count} actions en attente` `` (aria-label, alors que `header.pendingActions` existe) |
| `sampling-plan-detail.component.ts:440` | `'Error generating orders'` |
| ~10 catch | fallback `'Error'` brut |
| `footer.component.ts:48` | `` `commit ${...}` `` (tooltip) |

**Proposition** : clés `errors.saveFailed`, `errors.generic`, réutiliser `header.pendingActions` ; intégrer un lint (eslint-plugin-ngx-translate ou règle maison) pour bloquer les littéraux UI.

**Effort** : S

### F-025 — Clés i18n orphelines et legacy

**Fichiers** : `assets/i18n/fr.json`, `de.json`, `en.json`

- 4 clés fr jamais référencées : `notifications.type.OrderAssigned`, `notifications.type.RoundAssigned`, `notifications.type.ResultsReceived`, `notifications.type.NonConformResult` (la cloche affiche `n.title` brut du backend).
- 12 clés présentes en de/en mais absentes de fr — résidus de l'ancien workflow : `orders.status.draft/assigned/sentToLims/resultsReceived/samplingCompleted/validated` + `orderStatus.descriptions.*` équivalents.

**Proposition** : purger lors de la complétion DE/EN (F-005) ; le script de diff CI couvrira les deux sens.

**Effort** : XS

### F-026 — Dates de notifications : `toLocaleString()` dépendant du navigateur

**Fichier** : `components/notifications-bell/notifications-bell.component.ts:183-189`

```ts
formatDate(iso: string): string {
    try {
      return new Date(iso).toLocaleString();   // « 6/11/2026, 3:04:00 PM » sur un navigateur en-US
```

**Raison** : seule zone de l'app qui formate une date hors `DatePipe` ; le rendu dépend de la locale OS du navigateur, pas de la langue de l'app. Non conforme DD.MM.YYYY.

**Proposition** : `{{ n.createdAt | date:'dd.MM.yyyy HH:mm' }}` dans le template (DatePipe importable), supprimer `formatDate`.

**Effort** : XS

### F-027 — `exportCsv` / `exportPdf` : aucune gestion d'erreur ni état de chargement

**Fichiers** : `pages/orders/order-list.component.ts:591-605`, `pages/sampling-locations/sampling-location-list.component.ts:317-326`

```ts
async exportCsv(): Promise<void> {
    const blob = await firstValueFrom(this.orderApi.exportCsv({ ... }));  // throw → unhandled
    const url = URL.createObjectURL(blob);
    ...
}
// location-list : .subscribe(blob => {...}) sans error handler
```

**Raison** : sur erreur serveur, rien ne se passe pour l'utilisateur (rejet non géré) ; sur exports longs, aucun spinner ni désactivation du bouton (double-clic = double requête).

**Proposition** : try/catch + toast + signal `exporting` désactivant le bouton ; mutualiser le téléchargement blob (duplication du code `createElement('a')` dans 2 fichiers).

**Effort** : XS

### F-028 — `sampling-location-form-dialog` : `valueChanges.subscribe` sans `takeUntilDestroyed`

**Fichier** : `pages/sampling-locations/sampling-location-form-dialog.component.ts:176-180`

```ts
this.form.get('distributorId')!.valueChanges.subscribe((distributorId: string) => {
  this.loadSectorsForDistributor(distributorId);
  this.form.get('sectorId')!.setValue('');
});
```

**Raison** : seul `subscribe` à flux infini du code applicatif sans teardown (les `afterClosed()` complètent d'eux-mêmes ; layout utilise `takeUntilDestroyed`). Pas de fuite réelle (le flux appartient au composant détruit) mais violation de la convention et fragilité : le handler reset `sectorId` aussi pendant le `patchValue` d'édition (ordre d'émission heureux aujourd'hui, cassable demain).

**Proposition** : `.pipe(takeUntilDestroyed(this.destroyRef))` + `setValue('', { emitEvent: false })` dans le patch d'édition, ou comparaison ancienne/nouvelle valeur.

**Effort** : XS

### F-029 — Liste des LDP : filtrage/pagination 100 % client sur `loadAll()`

**Fichier** : `pages/sampling-locations/sampling-location-list.component.ts:200-206, 330+`

```ts
searchText = '';
selectedDistributorId = '';
selectedStatus = '';
pageSize = 25;
currentPage = 0;
readonly filteredLocations = signal<SamplingLocationDto[]>([]);
```

**Raison** : contrairement aux autres listes (filtres serveur paginés), les LDP chargent tout le référentiel puis filtrent/paginent en mémoire avec 5 propriétés mutables non-signals et un `applyFilters()` manuel rappelé après chaque dialog. Incohérence d'architecture + non scalable (le canton compte des centaines de LDP) + état non réactif (OnPush fonctionne par effet de bord des handlers).

**Proposition** : aligner sur le pattern datastore filtré serveur (le backend expose déjà filtres/pagination pour les autres entités) ou a minima convertir l'état en signals + `computed`.

**Effort** : M

### F-030 — `order-detail.loadOrder` sans catch : page blanche sur 404/erreur

**Fichier** : `pages/orders/order-detail.component.ts:206-217`

```ts
this.loading.set(true);
try {
  const data = await firstValueFrom(this.orderApi.getById(id));
  this.order.set(data);
} finally {
  this.loading.set(false);
}
```

**Raison** : sur 404 (mandat supprimé, lien notification périmé) ou erreur réseau, l'exception remonte non gérée ; le template `@if (loading()) ... @else if (order())` ne rend **rien** → page entièrement blanche sans message ni bouton retour. Même pattern dans `sampling-round-detail.ngOnInit:512-518` et `sampling-plan-detail`.

**Proposition** : catch + signal `error` + bloc `@else { message « introuvable » + bouton retour }`.

**Effort** : S (3 pages détail)

### F-031 — `console.*` en production, ngx-logger absent malgré CLAUDE.md

**Fichiers** : 10 occurrences — `services/auth.service.ts:99,163,166`, `services/sync.service.ts:270`, `pages/sampling-rounds/sampling-round-detail.component.ts:939`, `sampling-form-dialog.component.ts:353`, `components/barcode-scanner/barcode-scanner-dialog.component.ts:128,137,147,173`

**Raison** : CLAUDE.md référence `ngx-logger` comme standard de logging Angular, mais la dépendance n'est même pas dans `package.json` ; les logs diagnostic offline/scanner partent en console sans niveau ni gating environnement (bruit + fuite d'ids internes en prod).

**Proposition** : soit introduire réellement ngx-logger (niveau WARN en prod), soit créer un mini `LogService` gateé par `environment.production` et mettre CLAUDE.md à jour.

**Effort** : S

### F-032 — TODO résiduels actifs

**Fichiers** :
- `pages/sampling-rounds/sampling-round-detail.component.ts:507` — `// TODO: handle create mode if needed` (route `new` rend une page vide sans message).
- `services/sync.service.ts:243` — `// TODO AQ-376 — plumbing ready; endpoint exists…` (REPLACE_LOCATION).
- `services/sync.service.ts:253` — `// TODO AQ-376 — no dedicated endpoint yet…` (COMPLETE_ORDER passe par /transition).

**Raison** : les deux TODO sync décrivent des décisions prises (endpoints branchés) — commentaires périmés ; le TODO round-detail correspond à un chemin mort (`id === 'new'` n'est créé nulle part).

**Proposition** : purger les commentaires, supprimer la branche `id === 'new'` ou la transformer en redirect.

**Effort** : XS

### F-033 — `SamplingRoundStatusColors` : export mort avec hex dans un modèle

**Fichier** : `models/sampling-round.model.ts:17-23`

```ts
export const SamplingRoundStatusColors: Record<SamplingRoundStatus, string> = {
  [SamplingRoundStatus.Draft]: '#455A64',
  [SamplingRoundStatus.Assigned]: '#0277BD',
  ...
```

**Raison** : plus aucun usage (les chips passent par `StatusChipVariant` depuis AQ-423) ; des couleurs en dur dans la couche modèle contredisent le design system.

**Proposition** : supprimer l'export.

**Effort** : XS

### F-034 — Code mort dispersé dans les composants

**Occurrences vérifiées** :
- `pages/home/home.component.ts:266-272` — computed `isPreleveur` jamais utilisé dans le template ; `pendingResultsCount` (l.244, 344) settée mais jamais affichée.
- `components/sidebar/sidebar.component.ts:270-274` — computed `isPreleveur` jamais utilisé (seuls `isAdmin`/`isPreleveurOnly` servent).
- `pages/results/results-matrix.component.ts:315-318` — `conformityBadge()` auto-documenté « not used in template ».
- `pages/results/sampling-results-dialog.component.ts:71-75` — `dialogRef` injecté et `close()` jamais appelés (le template utilise `mat-dialog-close`).

**Proposition** : suppression simple.

**Effort** : XS

### F-035 — Header : transmission/validation snapshot non rafraîchis après actions bulk… (compteurs dashboard figés)

**Fichier** : `pages/home/home.component.ts:274-336`

**Raison** : tous les compteurs du dashboard sont chargés une seule fois en `ngOnInit` (pattern fire-and-forget, erreurs avalées « Silently handle error » ×4). Pas de rafraîchissement au retour sur l'écran (le composant est recréé à chaque navigation, OK) mais surtout : en cas d'échec API, tuiles à 0 sans aucun indicateur — un dashboard de pilotage qui affiche « 0 non conformes » sur erreur réseau est trompeur pour un service cantonal.

**Proposition** : distinguer visuellement « 0 » de « erreur de chargement » (tiret + tooltip), centraliser les 4 catch.

**Effort** : S

### F-036 — `interceptor` : lecture du token via `sessionStorage` direct, dupliquant la source de vérité

**Fichier** : `handlers/auth.interceptor.ts:16-21` vs `services/auth.service.ts:44,171-173`

```ts
let token: string | null = null;
try {
  token = sessionStorage.getItem('access_token');
} catch { ... }
```

**Raison** : l'`AuthService` expose `accessToken` (signal) et `getToken()`, mais l'interceptor lit directement sessionStorage avec une clé dupliquée en littéral (`'access_token'` vs constante `SESSION_ACCESS_KEY` privée du service). Si la clé ou la stratégie de stockage change (cf. AQ-409 déjà passé par là), l'interceptor peut diverger silencieusement. Le commentaire invoque la performance, non mesurée (lecture signal = négligeable).

**Proposition** : `injector.get(AuthService).getToken()` (l'injection lazy est déjà en place pour le 401) ou a minima exporter la constante de clé depuis `auth.service.ts`.

**Effort** : XS

### F-037 — Dialogs : largeurs fixes en dur recopiées partout

**Occurrences** : `width: '550px'` (5×), `'500px'` (6×), `'450px'`, `'400px'` (4×), `'560px'`, `'95vw'/maxWidth '720px'`, `'900px'/maxWidth '95vw'`… réparties dans 12 composants appelants, avec `panelClass: 'responsive-dialog'` présent ou absent de façon aléatoire (p.ex. `replace-location-dialog` et `assign-sampler-dialog` ouverts sans panelClass responsive, `round-detail.ts:662-665, 719-722`).

**Raison** : incohérence responsive (certains dialogs débordent sur mobile faute de panelClass), maintenance dispersée.

**Proposition** : constantes partagées (`DIALOG_SM/MD/LG`) incluant `panelClass`, ou helper `openResponsiveDialog()`.

**Effort** : S

### F-038 — `round-detail.goBack()` toujours vers la liste, `order-detail` heuristique `history.length`

**Fichiers** : `pages/sampling-rounds/sampling-round-detail.component.ts:1086-1088` ; `pages/orders/order-detail.component.ts:381-393`

```ts
// round-detail : retour fixe
goBack(): void { this.router.navigate(['/sampling-rounds']); }
// order-detail (AQ-426) :
if (window.history.length > 1) { this.location.back(); return; }
```

**Raison** : deux stratégies de retour différentes pour deux pages détail jumelles. `history.length > 1` est presque toujours vrai (compte tout l'historique de l'onglet, y compris hors app) → `location.back()` peut sortir de l'application si l'utilisateur est arrivé par lien direct/notification, contournant le fallback prévu.

**Proposition** : suivre l'état de navigation Angular (`Router.getCurrentNavigation()?.previousNavigation` mémorisé dans un service, ou passer `state: { from: ... }`) et unifier les deux pages.

**Effort** : S

---

## 5. Findings — Basse

### F-039 — Conversion design tokens incomplète : 34 composants avec couleurs hex en dur

**Mesure** (grep `#hex` dans les styles inline, hors fichier généré) :

| Composant | Occurrences hex |
|---|---|
| `pages/results/recent-results-zone.component.ts` | 22 |
| `pages/results/results-matrix.component.ts` | 20 |
| `components/sampling-results-table/sampling-results-table.component.ts` | 10 |
| `components/notifications-bell/notifications-bell.component.ts` | 7 |
| `pages/sampling-rounds/sampling-form-dialog.component.ts` | 6 |
| `pages/orders/order-detail.component.ts`, `assign-sampler-dialog` | 5 |
| + 27 autres fichiers | 1–4 |

Seuls **11 fichiers** consomment `var(--token)` (login, home, order-list, header, sidebar, layout, status-chip, round-detail, results.component…). Tout l'écran Résultats (AQ-415) et les dialogs sont restés en hex Material 2 (`#E8F5E9`, `#FFEBEE`, `#666`…), alors que `_tokens.scss` définit 138 tokens dont les `--chip-*` et `--conformity-*` correspondants.

**Proposition** : terminer la migration AQ-422+ écran par écran, en commençant par results-* (plus gros volume) ; interdire les hex en lint (stylelint `color-no-hex` sur les styles inline).

**Effort** : M

### F-040 — `order-detail` : styles entièrement pré-design-system (px + hex, aucun token)

**Fichier** : `pages/orders/order-detail.component.ts:171-186`

```scss
.detail-item label { font-size: 12px; color: #666; text-transform: uppercase; }
.notes-section { padding: 16px; border-top: 1px solid #e0e0e0; ... }
```

**Raison** : page métier centrale jamais passée au refresh v0.94 (ni espaces `--space-*`, ni `--color-*`, ni max-width 1440 ni page-title standard) — rupture visuelle avec orders-list/home refaits en AQ-424.

**Proposition** : aligner sur le gabarit des pages refaites (header standard + tokens).

**Effort** : S

### F-041 — `aria-label` hard-codés en anglais (10 occurrences)

**Fichiers** : `header.component.ts:30,34,79,95,118` (« Toggle navigation menu », « AquaPlan home », « Change language », « User menu »), `sidebar.component.ts:19` (« Main navigation »), `sampling-results-dialog.component.ts:37` (« Close »), `order-indicators.component.ts:40,48,56` (identifiants techniques).

**Raison** : app FR/DE — les lecteurs d'écran annoncent de l'anglais. Le reste du code utilise correctement `[attr.aria-label]="'…' | translate"` (notifications-bell, round-detail).

**Proposition** : basculer en clés i18n (`a11y.*`).

**Effort** : XS

### F-042 — Icônes décoratives sans `aria-hidden` (incohérent)

**Constat** : `home.component.ts` marque ses `mat-icon` décoratifs `aria-hidden="true"`, mais les mêmes icônes décoratives dans `sidebar`, `order-list`, `round-detail`, `login` ne le sont pas → verbosité lecteur d'écran.

**Proposition** : `aria-hidden="true"` systématique sur les icônes purement décoratives (règle de revue).

**Effort** : XS

### F-043 — `app.component.ts` : seul composant sans `OnPush`

**Fichier** : `app.component.ts:6-11`

```ts
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [],
})
```

**Raison** : impact perf nul (template = router-outlet) mais brèche dans la règle « OnPush sur tous les composants » — visible dans tout audit automatisé.

**Proposition** : ajouter `changeDetection: ChangeDetectionStrategy.OnPush`.

**Effort** : XS

### F-044 — Doublon d'initialisation i18n : `app.component` vs `app.config` vs `LocaleService`

**Fichiers** : `app.config.ts:44-47` (`defaultLanguage: 'fr'`), `app.component.ts:16-19` (`setDefaultLang('fr'); use('fr')`), `services/locale.service.ts:9-13` (`addLangs; setDefaultLang('fr'); use('fr')`)

**Raison** : trois endroits posent la même configuration ; `LocaleService` n'est instancié qu'à la première injection (header) — l'ordre réel d'exécution est fragile. La langue choisie n'est par ailleurs **pas persistée** (retour à `fr` à chaque rechargement, y compris pour un utilisateur DE).

**Proposition** : centraliser dans `LocaleService` provisionné via `provideAppInitializer`, persister la langue (IndexedDB/sessionStorage) et supprimer le constructeur d'`AppComponent`.

**Effort** : S

### F-045 — `home.component.ts` : indentation 4 espaces (reste du code = 2)

**Fichier** : `pages/home/home.component.ts` (tout le fichier)

**Raison** : seule exception du dossier ; gêne les diffs/reviews. Aucun `.editorconfig`/prettier config trouvé pour ClientApp.

**Proposition** : reformater + ajouter prettier/editorconfig (2 espaces TS/HTML/SCSS).

**Effort** : XS

### F-046 — `LocaleService` : injection par constructeur (convention `inject()`)

**Fichier** : `services/locale.service.ts:9`

```ts
constructor(private readonly translate: TranslateService) {
```

**Raison** : unique service du code en constructor injection ; la convention projet (CLAUDE.md « nouveaux codes ») impose `inject()`.

**Proposition** : `private readonly translate = inject(TranslateService);`.

**Effort** : XS

### F-047 — `@ViewChild` au lieu de `viewChild()` signal dans du code récent

**Fichiers** : `components/layout/layout.component.ts:63` (`@ViewChild('sidenav') sidenav!: MatSidenav;`), `components/barcode-scanner/barcode-scanner-dialog.component.ts:57`

**Raison** : composants retouchés en v0.94 (AQ-423) / AQ-404 ; l'API signal `viewChild.required<MatSidenav>('sidenav')` est la cible pour le nouveau code.

**Proposition** : migrer ces deux occurrences.

**Effort** : XS

### F-048 — `barcode-scanner-dialog` : `ngAfterViewInit` sans `implements AfterViewInit`

**Fichier** : `components/barcode-scanner/barcode-scanner-dialog.component.ts:56,68`

```ts
export class BarcodeScannerDialogComponent implements OnDestroy {
  ...
  ngAfterViewInit(): void {   // hook appelé par convention de nom, interface non déclarée
```

**Raison** : fonctionne (Angular résout par nom) mais le contrat d'interface manquant désamorce la vérification de typage et trompe le lecteur.

**Proposition** : `implements AfterViewInit, OnDestroy`.

**Effort** : XS

### F-049 — `login.component` : état de formulaire en propriétés mutables

**Fichier** : `pages/login/login.component.ts:231-232`

```ts
email = '';
password = '';
```

**Raison** : page refaite en AQ-424 mais l'état reste en propriétés brutes + `[(ngModel)]` ; le reste du code de la même époque utilise signals ou Reactive Forms. Pas de validation d'email côté client.

**Proposition** : Reactive Form typé (`email: [Validators.required, Validators.email]`).

**Effort** : XS

### F-050 — `results-matrix` : filtres en propriétés mutables non-signals

**Fichier** : `pages/results/results-matrix.component.ts:252-254`

```ts
distributorId: string | null = null;
sectorId: string | null = null;
anomaliesOnly = false;
```

**Raison** : composant OnPush récent (AQ-415) — l'état des filtres n'est pas réactif (le rendu repose sur les événements `selectionChange`). Convention signals non respectée pour du nouveau code.

**Proposition** : convertir en `signal()` + `effect`/appel explicite.

**Effort** : XS

### F-051 — `order-list` : `provideNativeDateAdapter()` re-provisionné au niveau composant

**Fichier** : `pages/orders/order-list.component.ts:43`

```ts
providers: [provideNativeDateAdapter()],
```

**Raison** : déjà fourni globalement dans `app.config.ts:41` — doublon inutile qui crée une seconde instance d'adapter pour ce sous-arbre.

**Proposition** : supprimer le provider local.

**Effort** : XS

### F-052 — `header` : libellé utilisateur « X. Nom » construit à la main sans fallback email

**Fichier** : `components/header/header.component.ts:253-258`

```ts
const initial = user.firstName?.charAt(0).toUpperCase() ?? '';
return `${initial}. ${user.lastName}`;
```

**Raison** : si `firstName` est vide, rend « . Dupont » ; aucune réutilisation du `greetingName()` plus robuste de `home.component.ts:253-264` (duplication de la même logique avec deux qualités différentes).

**Proposition** : utilitaire commun `formatUserDisplayName(user)` (models/user ou AuthService).

**Effort** : XS

### F-053 — Tables Material : maps `displayedColumns`/structure de page répétées sur 9 listes

**Fichiers** : `sector-list`, `distributor-list`, `user-list`, `role-list`, `analysis-{programs,profiles,containers}`, `sampling-plan-list`, `delegation-list`

**Raison** : les 9 écrans de référentiel partagent le même squelette (page-header + bouton créer admin + filtre recherche + `mat-table` + dialog form + reload après `afterClosed`) recopié intégralement — toute évolution transverse (tri, pagination, état vide, a11y) coûte 9 modifications. C'est de la duplication structurelle (> 3 composants), pas encore factorisée.

**Proposition** : composant générique `EntityListPage` (ou au moins partials : en-tête standard + état vide + wrapper table responsive) — sans sur-ingénierie : commencer par les 3 pages catalogue quasi identiques.

**Effort** : L (générique) / M (catalogue seul)

### F-054 — `confirm` natif côté liste vs `ConfirmDialog` côté dialog pour la **même** action LDP

**Fichiers** : `pages/sampling-locations/sampling-location-list.component.ts:217` (delete via `confirm()`) vs `pages/sampling-locations/sampling-location-form-dialog.component.ts:206-219` (même delete via `ConfirmDialogComponent`)

**Raison** : la suppression d'un LDP présente deux UX différentes selon le point d'entrée. Cas concret du problème générique F-011.

**Proposition** : couvert par F-011 (unifier sur ConfirmDialog).

**Effort** : XS

### F-055 — `styles` inline volumineux dans les composants de page (jusqu'à ~90 lignes)

**Fichiers** : `sampling-round-detail.component.ts:333-422` (~90 lignes), `order-list.component.ts:211-300` (~90 lignes), `home.component.ts:183-231` (~48 lignes denses, sélecteurs mono-ligne multiples), `login.component.ts:82-224` (~140 lignes)

**Raison** : sous le seuil des 200 lignes fixé par l'audit pour exiger un `_styles.scss`, sauf `login.component.ts` (~142 lignes) qui s'en approche ; le budget `anyComponentStyle` (4 kB warn) est par ailleurs déjà tutoyé par login/round-detail. Lisibilité TS dégradée.

**Proposition** : extraire au minimum `login.component.scss` (`styleUrl`) ; pour les autres, mutualiser les blocs récurrents (`.page-header`, `.loading-container`, `.no-data`, `.responsive-table-container` — recopiés dans ~12 composants) dans `styles.scss`.

**Effort** : S

### F-056 — `user-form-dialog` / dialogs admin : `ngOnInit` séquentiel et sans gestion d'erreur

**Fichier** : `pages/admin/users/user-form-dialog.component.ts:141-160` (et `order-create-dialog.component.ts:266-278`)

```ts
const allRoles = await firstValueFrom(this.roleApi.getAll());
this.roles.set(allRoles);
if (this.data.mode === 'create') {
  const allDistributors = await firstValueFrom(this.distributorApi.getAll({ isActive: true }));
  ...
}
if (this.data.mode === 'edit' && this.data.userId) {
  const user = await firstValueFrom(this.userApi.getById(this.data.userId));
```

**Raison** : appels en série (latence cumulée à l'ouverture du dialog) et aucune erreur gérée : un échec laisse un formulaire vide/incomplet sans message, soumissible.

**Proposition** : `Promise.all` pour les référentiels + catch avec fermeture du dialog et toast.

**Effort** : XS

---

## 6. Patterns récurrents

1. **Duplication de logique de rôles** — `isAdmin` recopié dans 8 composants, `isPreleveur` en 3 variantes sémantiquement différentes (F-012). Cause racine : `AuthService` n'expose qu'un seul computed métier (`canCreateOrders`). Tout durcissement des règles d'affichage passe aujourd'hui par N fichiers.

2. **Gestion d'erreur en îlots** — trois familles coexistent : try/catch+snackBar verbeux (round-detail, 8×), catch silencieux « Silently handle error » (home, datastores, results), et absence totale de catch (dialogs de création/édition, exports, plan-detail). Aucune normalisation des messages (codes API bruts, fallback `'Error'`). → un `ApiErrorService` + un signal `error` par datastore résoudraient F-014/017/021/027/030/035/056 d'un coup.

3. **Datastores singletons à état ouvert** — signals writable publics, filtres persistants entre navigations sans reset, `loadFiltered()` fire-and-forget non protégé contre les courses (F-006/007/016/017). Le pattern est sain dans son principe (signals + API service) mais manque une couche de discipline (readonly + setters + reset + seq guard).

4. **Design system à mi-gué** — la migration tokens AQ-422/423/424 couvre le shell (header, sidebar, login, home, orders, rounds) mais pas l'écran Résultats, les dialogs, ni order-detail : 34 fichiers gardent des hex Material 2, deux fichiers gardent des `[style.color]` inline dans les templates (round-detail:233,270,282), et un export de couleurs mort traîne dans un modèle (F-033/039/040). Risque : le « refresh » v0.94 paraît terminé alors que la moitié des surfaces ne l'est pas.

5. **Offline robuste mais à angles morts de validation** — l'infrastructure (IDB, queue, replay, snapshot AQ-427) est de bonne qualité, mais les chemins qui *alimentent* la queue ne valident pas (F-002) et les rejets de replay détruisent la donnée (toast éphémère). La règle à instaurer : *tout payload mis en queue offline doit être valide par construction, et tout rejet de replay doit rester consultable*.

6. **i18n trilingue de façade** — fr complet, de/en à ~70 %, langue non persistée, dates au format US faute de LOCALE_ID, chaînes UI et aria-labels en dur (F-004/005/024/025/026/041/044). Pour un canton bilingue, le DE doit passer au même niveau d'exigence que le FR, avec garde-fous CI.

7. **Boilerplate de liste recopié 9×** — squelette page-header/recherche/table/dialog/reload dupliqué sur tous les référentiels, avec divergences accidentelles (LDP en filtrage client, debounce artisanal 3×, dialogs aux largeurs aléatoires) (F-015/029/037/053).

8. **Dette de configuration build** — Bootstrap chargé pour rien, double thème Material, client NSwag mort de 2 900 lignes, ngx-logger documenté mais absent (F-022/023/031). Le bundle initial paie ces choix sur chaque visite terrain, d'autant qu'aucune route n'est lazy (F-003).

---

## 7. Recommandations priorisées (quick wins → chantiers)

| Priorité | Actions | Findings couverts | Effort cumulé |
|---|---|---|---|
| P0 — immédiat | F-002 (garde `form.invalid`), F-008 (await OIDC), F-009 (cloche), F-004 (LOCALE_ID fr-CH) | 4 | ~1 jour |
| P1 — sprint courant | F-001 (tokens en query string, avec backend), F-006 (reset filtres), F-010 (attempts persistés), F-024/026 (chaînes dures) | 6 | ~3 jours |
| P2 — prochaine version | F-003 (lazy routes), F-005 (de.json), F-007 (seq guard), F-011/012/013/014 (factorisation transverse), F-023 (bootstrap/thème) | 10 | ~1 sprint |
| P3 — fond de backlog | F-022 (NSwag), F-029 (LDP serveur), F-039/040 (tokens restants), F-053 (liste générique) | reste | continu |
