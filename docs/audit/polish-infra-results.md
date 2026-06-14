# Sprint Polish — Infra / cross-cutting — Résultats

**Date** : 2026-06-14
**Rôle** : DevOps / Platform Engineer
**Base** : `Main` @ `d7c7890`
**Branche** : `feature/polish-infra`
**PR** : [#4](https://github.com/franko-devmaster/aquaplan/pull/4) (vers `Main`, **non mergée**)
**Remote** : `github` (`franko-devmaster/aquaplan`). Bitbucket (`origin`) **non poussé** (mort).
**Périmètre** : infra uniquement (yml, conf, Dockerfile, .gitignore, .dockerignore, docs, `.github/`)
+ un seul fichier backend autorisé (`Program.cs` pour le CORS). Pas de collision avec les
agents backend/frontend en parallèle.

---

## Commits (par thème)

| SHA | Titre |
|---|---|
| `2c04204` | chore(infra): F-038cc F-027cc — remove repo parasites (prototype, tmp logs) + gitignore guards |
| `502e91f` | infra(docker,nginx): F-023cc F-024cc F-035cc F-036cc — lean context, prod config, nginx hardening, healthchecks |
| `8ae669e` | ci(infra): F-032cc F-033cc F-034cc F-042cc — drop Bitbucket pipeline, add lint/audit job + Dependabot |
| `6ff233e` | docs(infra): F-040cc F-041cc — archive obsolete docs, point agents to GitHub (renames) |
| `f180b99` | docs(infra): F-040cc F-041cc — add archive banners + finalize agent GitHub refs |
| `e4cf022` | feat(api): F-026cc — make CORS allowed origins configurable (commit isolé) |

---

## Findings traités

| ID | Statut | Détail |
|---|---|---|
| **F-023cc** | ✅ | `.dockerignore` exclut `docs/`, `**/*.md`, `tests/e2e`, `.github`, `AquaPlan-prototype*.html`, `bitbucket-pipelines.yml`. **Les `tests/AquaPlan.*.csproj` sont conservés** car `dotnet restore AquaPlan.slnx` les référence — vérifié par un build Docker API réussi. |
| **F-024cc** | ✅ | `src/AquaPlan.Api/appsettings.Production.json` créé : `MockLims.Enabled`/`LimsSync` rendus **explicites** (gardés `true` pour la démo, surchargeables via env var), Serilog `Warning`. **Aucun secret** (env vars Render/NAS). |
| **F-026cc** | ✅ | CORS configurable : env var `CORS_ALLOWED_ORIGINS` (CSV) ou `Cors:AllowedOrigins` ; fallback `http://localhost:4200` en Dev seulement ; vide en Prod (le web appelle l'API same-origin via le proxy `/api/`). `AllowCredentials` conservé uniquement avec origines explicites. Build API Release OK. |
| **F-032cc** | ✅ | `bitbucket-pipelines.yml` supprimé. |
| **F-033cc** | ✅ (vérif) | Cache npm déjà correct dans `build-angular` (`setup-node` `cache: npm` + `cache-dependency-path`). Pas de régression, aucun correctif nécessaire. |
| **F-034cc** | ✅ | Nouveau job `lint-audit` **advisory** (`continue-on-error: true`) : `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive`, `npm audit --audit-level=high`. Non bloquant → le pipeline reste vert. |
| **F-035cc** | ✅ | nginx `server_tokens off;` ; `X-XSS-Protection` (obsolète) **retiré** ; **HSTS** ajouté (`max-age=31536000; includeSubDomains`). Les security-headers (CSP, etc.) du Sprint Sec étaient déjà en place. |
| **F-036cc** | ✅ | `HEALTHCHECK` (busybox `wget` — pas de curl dans les images alpine, vérifié) sur `api` (`/api/health`) et `web` (`/`) dans les **deux** compose ; web attend `aquaplan-api: service_healthy`. |
| **F-038cc** | ✅ | Supprimé : `AquaPlan-prototype.html` (tracké), `AquaPlan-prototype 2.html`, `.claude/scheduled_tasks 2.lock`. |
| **F-027cc** (rappel) | ✅ | `.tmp_workspace/*.json` (6 logs de debug **trackés**) untrackés + supprimés ; `.gitignore` complété (`.tmp_workspace/`, `AquaPlan-prototype*.html`, `.claude/scheduled_tasks*.lock`). |
| **F-040cc** | ✅ | `PLAN-DEVELOPPEMENT.md` et `docs/plan-integration-tests.md` déplacés dans `docs/archive/` + bandeau « OBSOLÈTE ». |
| **F-041cc** | ✅ (ciblé) | `jira-updater.md` : push vers `github` et `gh pr create` sur `franko-devmaster/aquaplan` (remplace l'URL API Bitbucket `francisuster/aquaplan`). `.claude/launch.json` ne contient aucune réf Bitbucket. |
| **F-042cc** | ✅ | `.github/dependabot.yml` : github-actions, nuget, npm (ClientApp + e2e), docker (API + Web), tous `weekly`. |

**Tous les findings du périmètre sont traités. Aucun reporté.**

---

## Validations

| Vérification | Résultat |
|---|---|
| `docker compose -f docker-compose.yml config` | ✅ valide |
| `docker compose -f docker-compose.synology.yml config` (env factices) | ✅ valide |
| `docker build -f src/AquaPlan.Api/Dockerfile .` (nouveau `.dockerignore`) | ✅ image construite (exit 0) |
| Build projet `AquaPlan.Api` en Release (worktree propre, changement CORS) | ✅ exit 0 |
| `.github/workflows/ci.yml` YAML | ✅ valide (pyyaml) |
| `.github/dependabot.yml` YAML | ✅ valide (pyyaml) |
| `appsettings.Production.json` structure JSON | ✅ valide (le provider .NET tolère commentaires + virgules finales) |

---

## Notes & limites

- **CORS / collision agent backend** : `Program.cs` était propre (non modifié par l'agent backend
  au moment de l'édition) ; le changement CORS est dans un **commit isolé** (`e4cf022`).
  Si l'agent backend touche aussi `Program.cs`, un conflit de merge mineur est possible — facile
  à résoudre (blocs distincts : eux sur les services métier, moi sur le bloc CORS).
- **Build solution complet non exécuté localement** : l'arbre de travail partagé contenait ~21
  fichiers `.cs` en cours d'édition par les agents backend/frontend. La validation du changement
  CORS a donc été faite dans un **worktree git détaché propre** au HEAD de la branche, pas dans
  l'arbre pollué.
- **Render** : non impacté. Le web Render est un site statique avec rewrite `/api/*` → API
  (same-origin), donc CORS vide en Prod convient. Pour exposer l'API en cross-origin, définir
  `CORS_ALLOWED_ORIGINS=https://aquaplan-web.onrender.com` sur le service API Render (opérationnel,
  non committé). HSTS/`server_tokens` ne concernent que l'image nginx (Synology) — sans effet sur
  le static site Render.
- **F-034cc volontairement advisory** : pas d'`.editorconfig` dans le repo + code historique non
  formaté → `dotnet format --verify-no-changes` flaggerait massivement. Le job entier est
  `continue-on-error` pour respecter « ne casse pas le pipeline existant ». À durcir une fois un
  `.editorconfig` ajouté.
- Le build Docker a confirmé l'alerte `NU1902` (OpenTelemetry.Api 1.15.1, vulnérabilité modérée) —
  exactement ce que le nouveau job `dotnet list package --vulnerable` fera remonter.

---

## Pas fait (hors périmètre, par design)

- Réécriture exhaustive de toutes les mentions « Bitbucket » en prose dans les fichiers agents
  (`dev-senior.md`, `qa-lead.md`, `team-lead.md`, `xray-tester.md`) : F-041cc cible la réf
  opérationnelle `francisuster/aquaplan` (corrigée). Les mentions conceptuelles sont laissées
  pour éviter un diff massif hors scope.
- High findings de l'audit (F-011 branches CI, F-012 versions, F-013 permissions/concurrency) :
  hors de la liste Medium assignée.
