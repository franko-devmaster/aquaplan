# Sprint Robustesse — Backend — Résultats

> Correction des 15 findings **High** du backend (rapport `docs/audit/backend-audit-fable.md`, F-101 à F-115).
> Date : 2026-06-13 — Branche `feature/sprint-robustesse-backend` depuis `Main` (`0a054ae`).
> Remote : `github` (`franko-devmaster/aquaplan`). **PR #2 — non mergée.**

## Synthèse

- **15 findings traités** (F-101 → F-115), **0 reporté**.
- Build `dotnet build AquaPlan.slnx --configuration Release` : **0 erreur**.
- Tests `dotnet test` : **994 / 994** (Application 22, Api 453, Infrastructure 519).
- 1 migration EF créée (`AddOrdersGeneratedAtToSamplingPlan`).
- PR : https://github.com/franko-devmaster/aquaplan/pull/2

## Commits par lot

| SHA | Lot | Titre |
|---|---|---|
| `0f78c0a` | J | `fix(oidc): F-101..F-102 — require email_verified + check IsActive at OIDC login` |
| `6b1d5aa` | I | `fix(robustness): F-109..F-111,F-114 — idempotence, OrderNumber race, lockout, logs auth` |
| `2176781` | H | `refactor(lims): F-105,F-108 — unify Completed→Transmitted in shared OrderTransmissionService` |
| `48db02f` | G | `fix(authz): F-103,104,106,107,112,113,115 — role + tenant scoping on write endpoints` |

## Détail par finding

### Lot G — Autorisation + scoping tenant
| Finding | Correction |
|---|---|
| **F-103** | `PermissionService.AssignRole/RemoveRole` valident le nom de rôle (`RoleName.All`) et exigent `user.TenantId == callerTenantId`. `RolesController` transmet le tenant de l'appelant. |
| **F-104** | `POST /orders/{id}/assign` : `[Authorize(Roles = Admin,Requérant,Requérant-Préleveur)]` + `UserCanAccessOrderAsync`. `OrderService` valide le préleveur cible (actif, même tenant, rôle Préleveur/Requérant-Préleveur). |
| **F-105 (authz)** | `POST /orders/{id}/transition` : contrôle d'accès au mandat identique à `UpdateOrder` (404/403). |
| **F-106** | `UpdateRound`, `DeleteRound`, `AssignPreleveur`, `TransmitAll`, `CancelRound`, `ReorderOrders` : rôles Admin+mandataire + `EnsureCanAccessRoundAsync` (scope distributeur via `IDelegationService`). |
| **F-107** | `DELETE /sampling-rounds/{id}` → **soft-cancel admin-only** : tournée `Cancelled`, mandats détachés (`SamplingRoundId = null`) et conservés ; tournée verrouillée → `RoundLockedException`, InProgress → `ConflictOperationException`. |
| **F-112** | Création LDP : rôles Admin+mandataire + scope distributeur ; `SamplingLocationService.CreateAsync` valide `DistributorId ∈ tenant`. |
| **F-113** | `SamplingLocationChangeRequestService` utilise `IDelegationService.GetAuthorizedDistributorIdsForUserAsync` (primaire + UserDistributors + délégations) ; message 403 générique (pas de fuite d'ids). |
| **F-115** | `POST .../sampling/validate` : rôles Admin+mandataire + `UserCanAccessOrderAsync`. |

### Lot H — Machine d'état Order + cohérence LIMS
| Finding | Correction |
|---|---|
| **F-105 (cohérence)** + **F-108** | Nouveau service partagé `IOrderTransmissionService` / `OrderTransmissionService` : unique implémentation de la transition `Completed → Transmitted` (statut + `StatusChangedAt/By` + `TransmittedAt` + push Mock LIMS/`LimsOrderId` + audit). Appelé par `OrderStatusService` (unitaire), `OrderService.BulkTransitionAsync` (bulk) et `SamplingRoundService.TransmitAllAsync` (tournée). Cause racine du backfill AQ-404 corrigée. Tests : les 3 chemins produisent le même état final. |

### Lot I — Idempotence, races, abus
| Finding | Correction |
|---|---|
| **F-109** | `SamplingPlan.OrdersGeneratedAt` (nullable) + check dans `GenerateOrdersFromPlanAsync`, posé dans la même transaction. Migration `AddOrdersGeneratedAtToSamplingPlan`. |
| **F-110** | `CreateOrderAsync` réessaie sur `DbUpdateException` (collision index unique `OrderNumber`, 5 tentatives) ; `GenerateOrderNumberAsync` utilise `int.TryParse` sur tous les numéros du jour (robuste au legacy). |
| **F-111** | `options.Lockout` : `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`. `AuthService.LoginAsync` → `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`. |
| **F-114** | `LogsController` : `[AllowAnonymous]` → `[Authorize]` (le front ne logge qu'en session connectée — non bloquant). `ClientLogDto` borné (`StringLength` sur Level/Message/Context). |

### Lot J — Durcissement OIDC
| Finding | Correction |
|---|---|
| **F-101** | `OidcUserService.FindOrCreateFromExternalLoginAsync` exige `emailVerified == true` avant de lier OU créer un compte local ; un utilisateur déjà lié par `ExternalId` reste accepté. `AuthController` lit la claim `email_verified`. |
| **F-102** | `AuthController.OidcCallback` refuse les comptes `IsActive == false` (redirige `account_disabled`), alignant le flux OIDC sur le login par mot de passe. |

## Tests xUnit ajoutés / modifiés

- **Nouveaux** : `PermissionServiceTest` (F-103), `SamplingsControllerTest` (F-115 + lacune F-223), `LogsControllerTest` (F-114).
- **Modifiés** : `OidcUserServiceTest` (+3 cas F-101), `AuthServiceTest` (mock `SignInManager` + cas lockout F-111), `OrderServiceTest` (F-104 préleveur valide/invalide, F-110 numéros, transmission via service partagé), `OrderStatusServiceTest` (F-105 transition Transmitted), `SamplingRoundServiceTest` (F-107 soft-cancel, F-108 transmission), `SamplingPlanServiceTest` (F-109 idempotence), `SamplingLocationChangeRequestServiceTest` (F-113 via délégation), `OrdersControllerTest` / `SamplingLocationsControllerTest` / `RolesControllerTest` (signatures + nouveaux contrôles).

## Migration EF

`20260613195036_AddOrdersGeneratedAtToSamplingPlan` — ajout colonne `orders_generated_at` (timestamptz, nullable) sur `sampling_plans`. `Up`/`Down` réversibles, additif, sans perte de données. Snapshot mis à jour.

## Problèmes rencontrés

- **`gh` CLI absent** : PR créée via l'API REST GitHub (token `.env`).
- **tmpfs `/private/tmp` plein** durant la session → `CLAUDE_CODE_TMPDIR` redirigé vers `~/.cache/claude_tmp`.
- **Warnings MSB4011** (≈100) dus à des fichiers macOS dupliqués `* 2.props/targets` dans les dossiers `obj/` (finding transverse F-027) — n'affectent pas le build (0 erreur), non corrigés ici (hors périmètre backend).
- **Refactor compatible** : la transition LIMS extraite dans `OrderTransmissionService` a nécessité d'ajuster les constructeurs de 3 services + leurs tests (un vrai service de transmission est câblé dans les tests, pas un mock, pour préserver les assertions d'intégration LIMS).

## Reporté

Aucun. Les 15 findings High sont traités.
