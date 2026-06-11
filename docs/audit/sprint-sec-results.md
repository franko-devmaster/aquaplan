# Sprint Sec — Résultats (audit Fable, 15 Critical)

> Branche : `feature/sprint-sec` (base `7e0c6a0` sur Main)
> Date : 2026-06-11
> Vérification : `dotnet build` 0 erreur, `dotnet test` **953/953** (21 Application + 431 Api + 501 Infrastructure), `ng build --configuration production` 0 erreur (1 warning budget SCSS préexistant), `tsc --noEmit` e2e OK.

## Synthèse

| # | Finding | Lot | Statut | Commit |
|---|---|---|---|---|
| 1 | F-001 backend / F-004 cc — JWT secret fallback hardcodé (code + appsettings + compose) | A | ✅ Corrigé | `320dd1c` |
| 2 | F-002 backend / F-005 cc — Admin/`Admin123!` seedé inconditionnellement | A | ✅ Corrigé (voir note force-change-password) | `320dd1c` |
| 3 | F-003 backend — Backfill LIMS cross-tenant via `TenantId` du body | B | ✅ Corrigé | `5c05ff5` |
| 4 | F-004 backend — `UserCreateDto.TenantId` fourni par le client | B | ✅ Corrigé | `5c05ff5` |
| 5 | F-005 backend — Bulk validate/transmit/finalize sans scoping distributeur | C | ✅ Corrigé | `882b0a3` |
| 6 | F-006 backend / F-001 frontend — Tokens OIDC en query string | D | ✅ Corrigé | `7855bb7` |
| 7 | F-001 frontend (mission) — Identifiants admin affichés dans le login | D | ✅ Déjà absent (vérifié) | — |
| 8 | F-002 frontend (mission) — Logs console PII en production | D | ✅ Corrigé | `7855bb7` |
| 9 | F-001 cc — `.claude/settings.local.json` tracké (secrets) | — | ✅ Déjà fixé (`7e0c6a0`), absence en HEAD vérifiée | `7e0c6a0` |
| 10 | F-004 cc — Mot de passe DB par défaut dans `docker-compose.synology.yml` | A | ✅ Corrigé | `320dd1c` |
| 11 | F-003 cc — JWT fallback dans `appsettings.json` | A | ✅ Corrigé (retiré complètement) | `320dd1c` |
| 12 | F-006 cc — Security headers nginx non servis sur le HTML (`add_header` shadow) | E | ✅ Corrigé | `6902f0e` |
| 13 | F-005 cc (mission) — HSTS preload sur DSM Reverse Proxy | — | 📝 Documenté, pas d'action (config DSM, hors repo, "pas grave si conscient") | — |
| 14 | F-009 cc — `deploy-synology.sh` restart sans recréation (ne déploie pas) | E | ✅ Corrigé | `6902f0e` |
| 15 | F-014 cc — ~90 step-defs e2e en auto-PASS → faux positifs Xray | F | ✅ Corrigé (90 steps → `pending`) | `ad41258` |

## Détail par lot

### Lot A — Secrets (`320dd1c`)
- **JWT** : `StartupSecurity.ResolveJwtSecret()` — crash au démarrage si `Jwt:SecretKey` absent hors Development (message explicite `JWT_SECRET requis`), secret aléatoire 512 bits éphémère en Development, longueur minimale 32 caractères imposée. Fallback supprimé de `Program.cs` ET de `appsettings.json`. Le secret résolu est réinjecté dans la configuration pour que `TokenService` signe avec la même clé que la validation.
- **Admin seed** : `admin@aquaplan.ch`/`Admin123!` seedé uniquement en Development/Test. En production : création one-shot depuis `INITIAL_ADMIN_EMAIL`/`INITIAL_ADMIN_PASSWORD` (no-op si le compte existe, warning loggé si aucun admin n'existe et que les variables manquent). Le mot de passe n'est jamais loggé.
- **Compose Synology** : `${DB_PASSWORD:?}` et `${JWT_SECRET:?}` requis (échec explicite), `INITIAL_ADMIN_*` passés en optionnel. Compose dev : `ASPNETCORE_ENVIRONMENT=Development` explicite pour préserver le seed dev/e2e.
- Tests : `StartupSecurityTest` (7), `RoleAndPermissionSeederTest` (7).
- ⚠️ **Action manuelle requise au prochain déploiement Synology** (reporté) : définir `DB_PASSWORD`, `JWT_SECRET`, `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD` dans le `.env` du NAS, faire tourner le secret JWT et le mot de passe DB actuels, changer le mot de passe du compte admin existant.

### Lot B — Cross-tenant (`5c05ff5`)
- `MockLimsBackfillRequestDto.TenantId` supprimé : le backfill opère exclusivement sur le tenant du JWT. Pas de scope `SuperAdmin` introduit (YAGNI — aucun besoin métier identifié).
- `UserCreateDto.TenantId` supprimé : `UsersController` passe `GetTenantId()` au service, qui force `user.TenantId`. Bonus sécurité : `Role` validé contre `RoleName.All`, `DistributorId` validé contre le tenant, annotations `[Required]`/`[EmailAddress]`/`[StringLength]` ajoutées.
- Frontend : le payload existant qui envoie encore `tenantId` est ignoré par le model binding (pas de breaking front).
- Tests : `UserManagementServiceTest` (8, nouveau fichier), mises à jour `AdminLimsSyncControllerTest` + `UsersControllerTest`.

### Lot C — Bulk scoping (`882b0a3`)
- `BulkValidateAsync`/`BulkTransmitAsync`/`BulkFinalizeAsync` prennent `bool isAdmin` ; `BulkTransitionAsync` filtre par `GetAuthorizedDistributorIdsForUserAsync` pour les non-admins (pattern AQ-398/AQ-420, identique à `GetOrdersFilteredAsync`).
- Controller : `isAdmin` dérivé de la permission `ViewAllOrders` (comme `GetOrders`).
- Tests : 5 tests de scoping service (non-admin restreint, délégation incluse, admin = tenant entier) + 2 tests controller sur le passage d'`isAdmin`.

### Lot D — OIDC + frontend (`7855bb7`)
- **OIDC** : le callback n'expose plus `?token=…&refresh=…`. Il génère un code 256 bits à usage unique (TTL 60 s, stockage mémoire singleton `OidcCodeExchangeService`) et redirige vers `/#/auth/callback?code=…`. Le SPA échange le code via `POST /api/auth/oidc-exchange` (nouveau endpoint `[AllowAnonymous]`, 401 si code inconnu/expiré/déjà utilisé).
  - ⚠️ Limite documentée : stockage in-memory = OK single-instance (Synology), à remplacer par un cache distribué pour la cible K8S multi-replicas (commentaire dans le code).
- **Login form** : vérifié — aucun placeholder/hint de credentials dans `login.component.ts` ; le message neutre `auth.contactAdmin` existe déjà en fr/de/en. Aucun changement i18n nécessaire.
- **Logs production** : helper `utils/dev-log.ts` (`devInfo` no-op si `environment.production`) appliqué aux `console.info` de `auth.service.ts` (3), `sync.service.ts` (1), `sampling-round-detail.component.ts` (1), `sampling-form-dialog.component.ts` (1). Les `console.warn/error` du scanner code-barres (diagnostics d'erreur caméra, sans PII) sont conservés.
- Tests : `OidcCodeExchangeServiceTest` (9), `AuthControllerTest` +4.

### Lot E — Infra (`6902f0e`)
- **nginx** : headers sécurité factorisés dans `security-headers.conf`, inclus au niveau `server` ET dans chacune des 8 locations qui déclarent leur propre `add_header` (héritage nginx cassé sinon). `always` partout → headers aussi sur les réponses d'erreur. Dockerfile + Dockerfile.ci copient le fichier. Vérification post-déploiement : `curl -sI https://aquaplan.synology.me/ | grep -i content-security`.
  - Note : `nginx -t` n'a pas pu être exécuté localement (ni nginx ni Docker disponibles) — à valider au premier build d'image CI.
- **deploy-synology.sh** : `docker restart` (qui gardait l'ancienne image) remplacé par `docker-compose up -d --remove-orphans` avec `TAG` propagé, après le pull. Script non exécuté (déploiement Synology reporté, conformément aux consignes).

### Lot F — e2e auto-PASS (`ad41258`)
- 90 steps neutralisés : `catchall.steps.ts` (66 stubs), `frontend.steps.ts` (13 gardes `!this.page`), `business-api.steps.ts` (6, dont l'assertion de statut warn-and-pass qui devient `pending` en cas de mismatch), `auth-roles.steps.ts` (5).
- `return 'pending'` → statut Cucumber PENDING → mappé TO DO côté Xray (mapping existant dans `xray-import.mjs`). Plus aucun PASSED fictif possible sur ces steps.
- Les vrais tests @smoke/@api/@ui (smoke, api, auth, navigation, infrastructure steps) sont intacts ; CI non impactée (e2e non exécutés en pipeline).

## Reporté Sprint Robustesse

| Sujet | Raison |
|---|---|
| **Force-change-password au premier login admin** (finding 2) | Nécessite un champ `MustChangePassword` sur `AppUser` + migration EF + UX front — exclu par la contrainte "pas de migration EF". Mitigation actuelle : compte créé one-shot depuis env vars que l'opérateur retire ensuite. |
| **Cache distribué pour les codes OIDC** (finding 6) | In-memory suffisant pour la cible single-instance actuelle ; requis seulement pour K8S multi-replicas (cf. F-019 cc `MigrateAsync` qui a la même contrainte). |
| **HSTS preload DSM** (finding 13) | Configuration DSM hors repo, déploiement Synology reporté. Conscient et documenté — à revoir lors du prochain passage sur le NAS. |
| **Implémentation réelle des 90 steps e2e** (finding 15) | Effort XL (audit). Le `pending` rend les trous visibles dans Xray ; implémenter en priorité les tests permissions/tenant (F-015 cc : comptes de test par rôle nécessaires). |
| **Comptes e2e par rôle** (F-015 cc, hors périmètre des 15) | Tous les rôles e2e pointent sur le compte admin — préalable à l'implémentation des steps RBAC. |
| **CSP durcissement** (F-008 cc, High) | `unsafe-eval`/`unsafe-inline`/`connect-src https:` conservés tels quels pour ne pas casser le bootstrap AQ-408 — maintenant que la CSP est réellement servie (finding 12), son contenu devient le prochain chantier. |
| **Rotation effective des secrets exposés** | Les valeurs `AquaPlan-Synology-2026!` / `AquaPlan-Prod-Secret-Key-Min-32-Chars!!` / `Admin123!` restent dans l'historique git : la rotation côté NAS (DB, JWT, admin) est indispensable au prochain déploiement. |

## Compteurs

- **Tests xUnit** : 953/953 verts (910 avant sprint, **+43 nouveaux**).
- **ng build production** : 0 erreur.
- **Commits** : 6 (un par lot), tous préfixés `fix(security)`/`fix(qa)` avec les IDs de findings.
