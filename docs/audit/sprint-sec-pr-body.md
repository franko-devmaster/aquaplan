## Sprint Sec — correction des 15 Critical findings de l'audit Fable

Base : `7e0c6a0` · Rapport détaillé : `docs/audit/sprint-sec-results.md`

### Lot A — Secrets (`320dd1c`) — findings 1, 2, 10, 11
- `Jwt:SecretKey` : **plus aucun fallback hardcodé**. Crash explicite au démarrage hors Development ; secret aléatoire éphémère en Development. Retiré d'`appsettings.json`.
- Admin `admin@aquaplan.ch`/`Admin123!` seedé **uniquement en Development/Test**. Production : création one-shot via `INITIAL_ADMIN_EMAIL`/`INITIAL_ADMIN_PASSWORD`.
- `docker-compose.synology.yml` : `DB_PASSWORD` et `JWT_SECRET` **requis** (`${VAR:?}`), plus de secrets committés.

### Lot B — Cross-tenant (`5c05ff5`) — findings 3, 4
- `MockLimsBackfillRequestDto.TenantId` supprimé : backfill scopé au tenant du JWT.
- `UserCreateDto.TenantId` supprimé : création dans le tenant de l'appelant, rôle validé contre `RoleName.All`, distributeur validé contre le tenant.

### Lot C — Bulk scoping (`882b0a3`) — finding 5
- `BulkValidate`/`BulkTransmit`/`BulkFinalize` : scoping distributeur AQ-398/AQ-420 pour les non-admins (`GetAuthorizedDistributorIdsForUserAsync`), admin = tenant entier.

### Lot D — OIDC + frontend (`7855bb7`) — findings 6, 7, 8
- Tokens OIDC **jamais en query string** : code à usage unique (256 bits, TTL 60 s) + `POST /api/auth/oidc-exchange`.
- Login form : vérifié sans placeholder de credentials (message neutre déjà en fr/de/en).
- `console.info` de diagnostic gatés derrière `environment.production` (`utils/dev-log.ts`).

### Lot E — Infra (`6902f0e`) — findings 12, 14
- nginx : security headers (CSP incluse) servis sur **toutes** les réponses, y compris le HTML (`security-headers.conf` inclus dans le serveur et chaque location avec `add_header`).
- `deploy-synology.sh` : recréation des conteneurs via `docker-compose up -d --remove-orphans` (le `docker restart` gardait l'ancienne image).

### Lot F — e2e (`ad41258`) — finding 15
- 90 step-definitions auto-PASS → `return 'pending'` (mappé TO DO dans Xray). Plus de faux positifs.

### Findings sans action code
- **#9** `.claude/settings.local.json` : déjà fixé en `7e0c6a0`, absence en HEAD vérifiée.
- **#13** HSTS preload DSM : config hors repo, documenté (déploiement Synology reporté).

### Testing
- [x] `dotnet build` — 0 erreur
- [x] `dotnet test` — **953/953** verts (+43 nouveaux tests)
- [x] `ng build --configuration production` — 0 erreur
- [x] `tsc --noEmit` (tests/e2e) — OK
- [ ] `nginx -t` — non exécutable localement (ni nginx ni Docker) → validé au premier build d'image
- [ ] Déploiement Synology — **reporté** (prérequis : `.env` NAS avec `DB_PASSWORD`, `JWT_SECRET`, `INITIAL_ADMIN_*` + rotation des secrets exposés)

### ⚠️ Breaking changes (volontaires, sécurité)
1. L'API **ne démarre plus** en Production sans `Jwt__SecretKey`.
2. `docker compose -f docker-compose.synology.yml` **échoue** sans `DB_PASSWORD`/`JWT_SECRET`.
3. Le compte admin par défaut n'est **plus créé** en Production (utiliser `INITIAL_ADMIN_*`).

**Ne pas merger automatiquement** — revue + validation user requises.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
