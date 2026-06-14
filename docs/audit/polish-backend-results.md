# Sprint Polish — Backend — Résultats (F-201 à F-230)

> Findings **Medium** backend de l'audit Fable (`backend-audit-fable.md`).
> Branche : `feature/polish-backend` (base `d7c7890` sur Main).
> Remote : `github` (`franko-devmaster/aquaplan`). Build Release : **0 erreur**. Tests : **1026/1026** xUnit.
> Périmètre strictement backend (src/AquaPlan.Api|Application|Infrastructure|Domain|Shared + tests).

## Synthèse

- **28 findings traités** sur 30 (F-201..F-230, hors F-223/F-224 reportés).
- 4 commits par lot, 1 migration EF (no-op de schéma), 1 nouveau helper partagé, 1 nouvelle exception métier.
- +~40 tests xUnit (authz, escaping, pagination, agrégation, validations, concurrence, dead-code).

## Commits (par lot)

| SHA | Titre |
|---|---|
| `2b521e8` | fix(security): F-204..F-208 F-228 F-202/203 — IDOR scoping + CSV escape + generic errors |
| `d399a02` | perf(backend): F-209..F-214 F-221 — N+1 removal, caching, split queries, pagination clamp |
| `13281bc` | fix(correctness): F-215..F-220 — lock release, optimistic concurrency, validation |
| `9271d95` | refactor(backend): F-201 F-222 F-225 F-227 — remove dead code + consolidate access logic |

## Lot 1 — Sécurité résiduelle

- **F-204** — `GET /api/orders/{id}/audit-log` : ajout du contrôle `UserCanAccessOrderAsync` (404/403) comme `GetOrder`.
- **F-205** — lectures intra-tenant scopées : `GetRound` (admin/préleveur-only/mandataire), `GetSampling` + `GetByBarcode` (accès commande), LDP `GetFiltered` (distributeurs autorisés pour non-admin).
- **F-206** — export PDF LDP : un `distributorId` explicite mais étranger est désormais refusé (IDOR) via `GetAuthorizedDistributorIdsForUserAsync`.
- **F-207** — `POST /api/samplingplans/{id}/submit` : ajout du `UserHasDistributorAccessAsync` (aligné sur Update/Delete).
- **F-208** — export CSV : échappement RFC-4180 (quoting + doublement des `"`) + neutralisation de l'injection de formules (`=`,`+`,`-`,`@` préfixés par `'`).
- **F-228** — `CreateOrderAsync` valide distributeur + LDP (appartenance au distributeur) + programmes contre le tenant (`ValidateOrderReferencesAsync`).
- **F-202** — nouvelle `BusinessRuleException : InvalidOperationException` mappée 400 (message exposé) ; `InvalidOperationException` nue → **500 générique** (message en logs). Tous les throws métier des services migrés vers `BusinessRuleException` (les `catch (InvalidOperationException)` des controllers continuent de fonctionner par héritage).
- **F-203** — `UnauthorizedAccessException` → 403 avec message générique « Accès refusé. » (détail en logs seulement).

## Lot 2 — Performance

- **F-209** — `GetUsersAsync` : projection unique avec jointure `UserRoles`/`Roles` en SQL (fin du N+1 `GetRolesAsync` par utilisateur) + filtre rôle en SQL.
- **F-210** — `PermissionService` (scoped) : cache mémoire par requête des permissions + requête unique `UserRoles → RolePermissions → Permission` (au lieu de 3-4 round-trips par check).
- **F-211** — `AsSplitQuery()` sur `SamplingRoundService.GetByIdAsync`/`TransmitAllAsync` et `OrderService.GetRequiredContainersAsync` (fin de l'explosion cartésienne).
- **F-212** — `GetMatrixAsync` borné : fenêtre 12 mois par défaut + cap dur 2000 mandats + `AsSplitQuery`.
- **F-213** — dashboard summary : 1 requête `GroupBy` agrégée (au lieu de N lignes + 2 sous-requêtes corrélées chacune).
- **F-214** — transitions bulk : `IOrderAuditService.LogRangeAsync` (un seul `SaveChanges` pour le batch).
- **F-221** — `PaginationGuard.Normalize` (Shared) : `page >= 1`, `pageSize ∈ [1,200]`, appliqué dans Orders/Rounds/LDP/Plans (fin du `OFFSET` négatif sur `page=0` et du chargement non borné).

## Lot 3 — Correctness

- **F-215** — `SamplingRound.MarkCompleted()` : source de vérité unique (statut + timestamps + **libération du verrou**) ; les 3 chemins de complétion (transition unitaire, bulk, transmit tournée) libèrent désormais le verrou préleveur.
- **F-216** — concurrence optimiste sur `SamplingRound` via le token système `xmin` (mapping shadow, **migration sans DDL** car colonne système) + `DbUpdateConcurrencyException` → 409 dans le middleware.
- **F-217** — `Create/UpdateUserAsync` : vérification des `IdentityResult` (`EnsureIdentitySucceeded`), validation du rôle en amont (plus de strip silencieux), retry sur collision d'unicité `UserNumber`.
- **F-218** — approbation change-request : unicité `LocationCode` vérifiée + `IsValidated = true` posé (apparition dans les snapshots offline).
- **F-219** — `DelegationService.CreateAsync` : distributeurs distincts, appartenance au tenant, cohérence des dates, pas de doublon actif.
- **F-220** — annotations de validation ajoutées aux DTOs d'écriture restants (Order create/update/assign, User update, Delegation create, Sampling create) alignées sur les `HasMaxLength` EF.

## Lot 4 — Cleanup

- **F-201** — suppression de `ApiExceptionFilterAttribute` (jamais enregistré) + son test ; correction des doc-comments des exceptions.
- **F-222** — suppression du dead code : `GetDelegatedDistributorIdsAsync` et la surcharge `GetUsersAsync(tenantId)`.
- **F-225** — `GenerateOrdersFromPlanAsync` : `CreateExecutionStrategy()` + transaction inconditionnelle (plus de détection du provider InMemory dans le code prod ; le test InMemory ignore `TransactionIgnoredWarning`).
- **F-227** — `IDelegationService.UserHasDistributorAccessAsync` devient la source de vérité unique ; `OrderService` et `SamplingPlanService` y délèguent (les deux ignoraient auparavant `AppUser.DistributorId`).
- **F-226** (traité en Lot 2) — `GetPreleveursAsync` filtre sur les noms de rôle exacts (`RoleName.Preleveur`/`RequerantPreleveur`) via la jointure `UserRoles`, fin du matching par sous-chaîne accentuée dans le controller.
- Nettoyage : suppression de 2 fichiers dupliqués macOS non suivis qui cassaient le build (`ForbiddenOperationException 2.cs`, `SamplingPlanService 2.cs`).

## Migration EF

- `20260614145939_AddSamplingRoundConcurrencyToken` — **no-op de schéma** : `xmin` est une colonne système PostgreSQL ; le `AddColumn` scaffolé a été retiré à la main (un `ALTER TABLE ... ADD COLUMN xmin` échouerait). La migration ne fait qu'enregistrer le changement de modèle dans le snapshot. Vérifié via `dotnet ef migrations script` : 0 ligne DDL.

## Findings reportés

- **F-223** (tests manquants `PermissionService`/`SamplingsController`/etc.) — **partiellement adressé** : tests ajoutés sur `SamplingsController` (via F-205) et la chaîne d'autorisation. La couverture exhaustive demandée (effort L) reste à compléter dans un sprint dédié.
- **F-224** (suite d'intégration Testcontainers-PostgreSQL) — **reporté** : effort XL, infrastructure de test, hors périmètre Polish backend. F-225 a supprimé la dépendance prod au harnais ; la divergence InMemory/PostgreSQL reste à couvrir par Testcontainers ultérieurement.
- **F-222 (projet `AquaPlan.Worker`)** — **conservé volontairement** : c'est une application exécutable et le foyer documenté (CLAUDE.md) des jobs d'arrière-plan. Le worker LIMS tourne actuellement comme hosted service dans l'API (`MockLimsResultsSyncWorker`). Sa suppression (édition `.slnx` + yml de déploiement) sort du périmètre backend-only / minimal-changes. Les deux méthodes mortes citées par F-222 ont, elles, été supprimées.

## Problèmes rencontrés

- **Répertoire de travail partagé avec l'agent Frontend** : le `git checkout -b` initial a réussi mais un process parallèle avait `feature/polish-frontend` checké out dans le même clone. Mes 4 commits backend (faits avec `git add -A`) ont atterri sur `feature/polish-frontend` en capturant aussi des fichiers `ClientApp` non commités de l'agent FE. Corrigé : `feature/polish-backend` reseté sur `d7c7890`, puis cherry-pick des 4 commits en restaurant systématiquement `src/AquaPlan.Web/ClientApp` à la base → branche finale **100 % backend** (vérifié : aucun fichier hors `AquaPlan.{Api,Application,Infrastructure,Domain,Shared}` / `tests/`). Leçon : `git add -A` est dangereux en répertoire partagé — préférer un add ciblé par chemins backend.
- **tmpfs `/tmp` saturé par intermittence** (process parallèle) — contourné par redirections vers `/tmp/*.log`.
- **`UseXminAsConcurrencyToken()` retiré dans Npgsql 10** — remplacé par `Property<uint>("xmin").IsRowVersion()` + migration manuelle no-op.
