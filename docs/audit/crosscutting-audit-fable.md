# Audit transverse AquaPlan — DevOps / Sécurité / CI-CD / E2E / Documentation

**Date** : 2026-06-11
**Auditeur** : Senior DevOps/Security Architect (Claude, modèle Fable 5)
**Périmètre** : tout ce qui n'est ni purement backend .NET ni purement frontend Angular — Docker, nginx, pipelines CI (Bitbucket + GitHub Actions), scripts, tests/e2e, docs, secrets, migrations EF, Worker.
**Branche auditée** : `Main` (HEAD `dbd69df`)
**Mode** : lecture seule (seul ce rapport a été créé).

---

## 1. Synthèse par sévérité

| Sévérité | Nb | Faits saillants |
|---|---|---|
| **Critical** | 7 | Token Jira API **committé et poussé** dans `.claude/settings.local.json` depuis le 1er commit ; PAT GitHub en clair dans l'URL du remote ; secrets prod (DB, JWT) en fallback dans `docker-compose.synology.yml` ; mot de passe admin `Admin123!` seedé en dur et utilisé en prod ; les security headers nginx (CSP incluse) **ne s'appliquent pas à `index.html`** (piège d'héritage `add_header`). |
| **High** | 14 | Script de déploiement Synology inopérant (`docker restart` ne charge jamais la nouvelle image) ; ~90 step-definitions e2e en `expect(true)` qui font passer artificiellement les tests Xray ; 46 features Gherkin v0.8→v0.91 jamais synchronisées vers `tests/e2e` ; CI GitHub ne couvre plus les branches hors Main ; 3 sources de version divergentes (0.94 / 0.93 / 0.0.0) ; Worker = stub de template ; 60 fichiers untracked dont toute la doc PO récente. |
| **Medium** | 14 | `.dockerignore` n'exclut ni docs/ ni tests/ ; MockLims actif par défaut sans appsettings.Production ; compose dev ≠ prod ; CORS hardcodé ; migrations EF avec SQL brut et Down destructif ; doublons macOS « * 2.* » ; `npm run report` cassé ; pas de `permissions:`/`concurrency` dans ci.yml. |
| **Low** | 8 | Pas de README racine ; TODO actifs (AQ-376) ; prototype HTML de 61 Ko à la racine ; doc projet pointant vers Confluence externe ; 3 variantes de scripts de seed redondantes ; en-tête « Canton de Vaud » dans un seed Fribourg. |

**Total : 43 findings.**

**Top 3 actions immédiates (aujourd'hui)** :
1. **Révoquer et régénérer** le token Jira `ATATT3x…90607DE9` (committé + poussé sur Bitbucket et GitHub), le PAT GitHub `github_pat_11CF2S…` et le secret Xray, puis purger l'historique git (F-001, F-002, F-003).
2. Changer le mot de passe admin de l'instance Synology et les secrets DB/JWT de prod (F-004, F-005).
3. Corriger `nginx.conf` pour que la CSP s'applique réellement au document HTML (F-006).

---

## 2. Findings — Critical

### F-001 — Token Jira API committé dans `.claude/settings.local.json` (et poussé)
**Fichier** : `.claude/settings.local.json` (fichier **tracké par git**, présent depuis le commit initial `6b9367d`)
```json
"Bash(curl -s -u \"francois.charriere@me.com:ATATT3xFfGF0x3JBW01e_Q3Xch_…=90607DE9\" … \"https://chfr.atlassian.net/rest/api/3/myself\")"
```
**Constat** : le token Jira Cloud apparaît **10 fois** dans la version committée (`git show HEAD:.claude/settings.local.json | grep -c ATATT` → 10). `git log -S` confirme sa présence depuis le **premier commit** du repo. Le repo est poussé sur Bitbucket (`origin`) et sur GitHub (`github`). Toute personne ayant accès en lecture à l'un des deux remotes possède un accès Jira complet au compte.
**Risque** : compromission du compte Atlassian (Jira + Confluence + Xray par rebond), modification/suppression de tickets, exfiltration de données projet.
**Proposition** :
1. Révoquer le token immédiatement dans id.atlassian.com (rotation déjà conseillée par la note mémoire « Jira test links unreliable »).
2. Ajouter `.claude/settings.local.json` au `.gitignore` (c'est un fichier *local* par définition) et `git rm --cached`.
3. Purger l'historique (BFG / `git filter-repo`) **sur les deux remotes**, ou considérer l'historique comme définitivement compromis et ne compter que sur la révocation.
**Effort** : S (révocation) + M (purge historique).

### F-002 — `.env` racine : 4 credentials actifs en clair sur le poste
**Fichier** : `.env` (non tracké — `.gitignore` OK — mais à la racine du repo)
```
JIRA_API_TOKEN=ATATT3xFfGF0x3JBW01e…   (le même que F-001)
XRAY_CLIENT_SECRET=5a2b3ddbb68da5a4e…
BITBUCKET_API_TOKEN=ATATT3xFfGF0_scu33…
GITHUB_TOKEN=github_pat_11CF2SLIY0iz2MXs7Ua7wD_…
```
**Constat** : le fichier n'est pas committé (vérifié : absent de `git ls-files` et de l'historique), mais il concentre 4 secrets longue durée en clair, sans `.env.example` documentant les variables attendues. Les scripts `tests/e2e/scripts/xray-*.mjs` le chargent par chemin relatif. Le token Jira étant identique à celui committé (F-001), il est de fait compromis.
**Proposition** : après rotation (F-001), stocker les secrets dans le trousseau macOS (`security find-generic-password` est déjà dans les permissions allow) ou un gestionnaire de secrets ; créer un `.env.example` sans valeurs ; durcir les droits (`chmod 600`, déjà le cas).
**Effort** : S.

### F-003 — PAT GitHub embarqué dans l'URL du remote git
**Constat** : `git remote -v` →
```
github  https://franko-devmaster:github_pat_11CF2SLIY0iz…@github.com/franko-devmaster/aquaplan.git
```
Le PAT est stocké en clair dans `.git/config`, visible par tout processus/outil local, copié dans chaque sauvegarde du dossier, et apparaît dans les logs de certains outils.
**Proposition** : `git remote set-url github https://github.com/franko-devmaster/aquaplan.git` + authentification via `gh auth login` ou le credential helper macOS. Révoquer le PAT actuel (il figure aussi dans `.env`).
**Effort** : XS.

### F-004 — Secrets de production en fallback dans `docker-compose.synology.yml` (committé)
**Fichier** : `docker-compose.synology.yml` lignes 9, 27–28
```yaml
POSTGRES_PASSWORD: ${DB_PASSWORD:-AquaPlan-Synology-2026!}
ConnectionStrings__DefaultConnection: "…Password=${DB_PASSWORD:-AquaPlan-Synology-2026!}"
Jwt__SecretKey: "${JWT_SECRET:-AquaPlan-Prod-Secret-Key-Min-32-Chars!!}"
```
**Constat** : les valeurs par défaut sont les secrets **réellement utilisés** si `DB_PASSWORD`/`JWT_SECRET` ne sont pas définis sur le NAS — et elles sont publiées dans le repo. Quiconque lit le repo peut forger des JWT valides (`Jwt__SecretKey` connu) ou se connecter à la DB si elle devient joignable.
**Proposition** : supprimer les fallbacks (`${DB_PASSWORD:?DB_PASSWORD requis}` — échec explicite), définir les secrets dans un `.env` local au NAS, faire tourner les secrets actuels.
**Effort** : S (compose) + S (rotation sur NAS).

### F-005 — Mot de passe admin par défaut `Admin123!` seedé par le code et utilisé en production
**Fichiers** : `src/AquaPlan.Infrastructure/Data/Seeds/RoleAndPermissionSeeder.cs:89` (`CreateAsync(admin, "Admin123!")`), `scripts/seed_data_synology.py` (login `Admin123!` sur `http://192.168.1.159:8880` = instance « prod » Synology), `tests/e2e/support/credentials.ts`, `tests/e2e/features/smoke.feature`, `scripts/seed_synology_docker.sh`.
**Constat** : le compte `admin@aquaplan.ch` / `Admin123!` est créé automatiquement à chaque démarrage de l'API (Program.cs ligne 156) — y compris en production — et le mot de passe est publié à 5 endroits du repo. L'instance Synology est exposée via `aquaplan.synology.me` (cf. commentaire nginx AQ-425).
**Proposition** : mot de passe initial injecté par variable d'environnement (`Admin__InitialPassword`) avec obligation de changement au premier login ; ne seeder l'admin par défaut qu'en `Development` ; changer le mot de passe sur l'instance publique immédiatement.
**Effort** : M.

### F-006 — nginx : les security headers (CSP comprise) ne s'appliquent PAS aux documents HTML
**Fichier** : `src/AquaPlan.Web/nginx.conf`
**Constat** : les headers de sécurité (lignes 91–96 : `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `Content-Security-Policy`) sont déclarés **au niveau `server`**. Or en nginx, `add_header` n'est hérité par un bloc `location` **que si celui-ci ne déclare aucun `add_header` lui-même**. Les blocs `location = /index.html`, `location = /`, `location = /ngsw-worker.js`, `location = /ngsw.json`, etc. déclarent tous des `add_header Cache-Control …` → ils **perdent l'intégralité des security headers**. Comme `location /` fait `try_files … /index.html` (redirection interne vers `location = /index.html`), **aucune page HTML de l'application n'est servie avec la CSP ni X-Frame-Options**. La CSP ne protège en pratique que les bundles JS/CSS, où elle est inutile.
**Proposition** : factoriser les security headers dans un fichier `security-headers.conf` et l'`include` dans **chaque** location qui fait `add_header`, ou répéter les 6 directives dans les blocs concernés. Vérifier ensuite avec `curl -sI https://aquaplan.synology.me/ | grep -i content-security`.
**Effort** : S.

### F-007 — Conteneur API exécuté en root, contexte de build = repo entier
**Fichier** : `src/AquaPlan.Api/Dockerfile`
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AquaPlan.Api.dll"]
```
**Constat** : aucune directive `USER` → l'application gouvernementale tourne en **root** dans le conteneur. De plus le stage build fait `COPY . .` (tout le repo, docs et tests compris — cf. F-022), pas de `COPY --chown`. L'image web (`nginx:alpine`) a aussi son master process en root (workers `nginx`), ce qui est le défaut nginx mais évitable.
**Proposition** : utiliser l'utilisateur fourni par les images .NET (`USER $APP_UID` ou `USER app`, supporté par les images .NET 8+) ; pour le web, envisager `nginxinc/nginx-unprivileged` (écoute 8080). Restreindre le `COPY` du stage build aux `*.csproj` + `src/` (et profiter d'un meilleur cache de layers).
**Effort** : S (API) + M (web, car le port interne change → compose/Synology à ajuster).

---

## 3. Findings — High

### F-008 — CSP laxiste : `unsafe-eval`, `unsafe-inline` (scripts) et `connect-src https:` ouvert
**Fichier** : `src/AquaPlan.Web/nginx.conf:96`
```
script-src 'self' 'unsafe-inline' 'unsafe-eval'; … connect-src 'self' https:;
```
**Constat** : (1) Angular en production AOT n'a pas besoin d'`unsafe-eval` — le commentaire parle de « dev helpers », non pertinent dans une image prod. (2) `unsafe-inline` est justifié par le bootstrap AQ-408 mais devrait être remplacé par un hash (`'sha256-…'`). (3) `connect-src https:` autorise des appels XHR/fetch vers **n'importe quel domaine HTTPS** → canal d'exfiltration idéal en cas de XSS. Conjugué à F-006 (CSP non appliquée), la défense en profondeur est nulle.
**Proposition** : `connect-src 'self'` (+ domaines explicites si besoin), retirer `unsafe-eval`, remplacer `unsafe-inline` par le hash du script inline.
**Effort** : S (+ tests Safari/iOS pour le bootstrap AQ-408).

### F-009 — `deploy-synology.sh` ne déploie jamais la nouvelle image
**Fichier** : `scripts/deploy-synology.sh:33-38`
```bash
ssh_cmd "sudo $DOCKER pull francoischarriere/aquaplan-api:$TAG && …"
ssh_cmd "sudo $DOCKER restart aquaplan-api && sudo $DOCKER restart aquaplan-web"
```
**Constat** : `docker restart` redémarre le conteneur existant **avec son image d'origine** ; le `pull` ne sert à rien. Pire : si on passe un tag précis (`./deploy-synology.sh abc1234`), l'image taguée est téléchargée mais le conteneur (créé sur `latest` figé) n'est jamais recréé. Le script donne l'illusion d'un déploiement réussi (le health-check passe… sur l'ancienne version).
**Proposition** : recréer les conteneurs via compose : `ssh … "cd /volume1/docker/aquaplan && TAG=$TAG sudo docker-compose up -d aquaplan-api aquaplan-web"` (c'est ce que fait correctement `synology-update.sh`). Vérifier la version déployée via le footer AQ-416 ou un endpoint `/api/version` plutôt qu'un simple 200.
**Effort** : S.

### F-010 — Usage de `sudo docker` à distance via SSH avec `StrictHostKeyChecking=accept-new`
**Fichier** : `scripts/deploy-synology.sh:28`
**Constat** : le script exécute `sudo docker …` sur le NAS via SSH en acceptant automatiquement une nouvelle clé d'hôte (`accept-new`) — vulnérable au MITM lors du premier contact, et il suppose un sudo sans mot de passe sur `docker` (équivalent root NAS). Un attaquant sur le LAN au premier déploiement peut intercepter la session.
**Proposition** : pré-provisionner la clé d'hôte (`ssh-keyscan` validé hors bande, `StrictHostKeyChecking=yes`), limiter sudo à un wrapper précis (`/volume1/docker/aquaplan/update.sh`) via sudoers plutôt qu'à `docker` générique.
**Effort** : S.

### F-011 — Migration GitHub Actions : les pushes hors `Main` n'ont plus de CI
**Fichiers** : `bitbucket-pipelines.yml` (pipeline `default:` = **toutes** les branches) vs `.github/workflows/ci.yml:7-11` (`push: branches: [Main]` + `pull_request: branches: [Main]`).
**Constat** : sur Bitbucket, chaque push sur n'importe quelle branche lançait build+tests. Sur GitHub, une branche de travail sans PR ouverte n'est **jamais buildée**. Régression du filet de sécurité.
**Proposition** : `on: push: branches: ['**']` (ou au minimum `Main` + `feature/**`), en gardant le job docker conditionné à Main (déjà le cas via `if:`).
**Effort** : XS.

### F-012 — Version applicative : 3 sources de vérité divergentes (0.94 / 0.93 / 0.0.0)
**Fichiers** : `.github/workflows/ci.yml:153` (`APP_VERSION=0.94.0-${tag}` **hardcodé**), `GitVersion.yml` (`next-version: 0.93.0`, commenté « Source of truth » AQ-416), `package.json` ClientApp (`0.0.0`).
**Constat** : le pipeline GitHub affiche 0.94.0 alors que la « source of truth » dit 0.93.0 ; `generate-version.mjs` lit GitVersion.yml en priorité hors CI → un build local affiche 0.93.0-sha et un build CI 0.94.0-sha. À chaque release il faudra penser à éditer ci.yml à la main (ça sera oublié).
**Proposition** : supprimer le hardcode du workflow et extraire la version de GitVersion.yml dans un step (`grep next-version GitVersion.yml`), ou pousser des tags git et laisser `git describe` faire foi. Mettre à jour GitVersion.yml à 0.94.x dès maintenant.
**Effort** : S.

### F-013 — ci.yml sans `permissions:` ni `concurrency:`
**Fichier** : `.github/workflows/ci.yml`
**Constat** : le `GITHUB_TOKEN` du workflow garde les permissions par défaut du repo (souvent write) alors que le pipeline n'a besoin que de `contents: read`. Aucun groupe `concurrency` → des pushes rapprochés sur Main lancent des builds docker concurrents qui se disputent le tag `latest` et le `buildcache`.
**Proposition** :
```yaml
permissions:
  contents: read
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```
**Effort** : XS.

### F-014 — ~90 step-definitions e2e « auto-PASS » : les résultats Xray sont faussés
**Fichiers** : `tests/e2e/step-definitions/catchall.steps.ts` (66 × `expect(true).toBeTruthy()`), `frontend.steps.ts` (13), `business-api.steps.ts` (6), `auth-roles.steps.ts` (5).
```ts
Then('le mandat est créé avec succès', async function () {
  expect(true).toBeTruthy();
});
```
**Constat** : des assertions métier entières (création de mandat, isolation tenant « il voit uniquement ses mandats », validation serveur, transitions de statut…) passent **inconditionnellement**. Combiné à `xray-import.mjs` qui pousse `PASSED` vers Xray, cela viole frontalement la règle CLAUDE.md : « *A test not executed in the app must be marked TO DO, never PASSED* ». La traçabilité QA Xray (AQ-194 non-régression incluse) est en partie fictive.
**Proposition** : transformer les stubs en `return 'pending'` (statut Cucumber `PENDING`, mappé `TO DO` côté Xray — le mapping existe déjà dans xray-import), puis implémenter progressivement les vraies assertions en commençant par les tests de permissions/tenant.
**Effort** : S (pending) / XL (vraies implémentations).

### F-015 — Tous les rôles de test e2e pointent vers le compte admin
**Fichier** : `tests/e2e/support/credentials.ts`
```ts
'preleveur': { email: 'admin@aquaplan.ch', password: 'Admin123!' },
'lecteur':   { email: 'admin@aquaplan.ch', password: 'Admin123!' },
```
**Constat** : « préleveur », « lecteur », « mandataire sie/gruyere/glane » = admin. Tout scénario Gherkin de restriction de droits ou d'isolation distributeur teste en réalité un admin omnipotent → faux positifs garantis sur les tests RBAC, domaine pourtant critique (application cantonale, multi-tenant).
**Proposition** : seeder des comptes de test par rôle (le commentaire du fichier l'annonce déjà : « may need password reset via seed script ») et les référencer ici ; faire échouer `getCredentials` si le rôle n'a pas de compte dédié plutôt que de retomber sur admin.
**Effort** : M.

### F-016 — Désynchronisation Gherkin totale : 0 feature v0.8/v0.9/v0.91 dans tests/e2e, et `features/xray/` est gitignoré
**Constat** :
- `docs/tests/gherkin/` contient 68 features (v0.1 : AQ-70..77, v0.2 : AQ-24..83, **v0.8 : AQ-303..313, v0.9 : AQ-333..345, v0.91 : AQ-369..379**).
- `tests/e2e/features/xray/` contient 43 features (AQ-128..190 — époques v0.3/v0.4), **aucun recouvrement** avec les clés des docs.
- `tests/e2e/.gitignore` ignore `features/xray/` → ces 43 fichiers n'existent **que sur ce poste** ; un clone frais n'a que 3 features committées (smoke, auth-api, login-ui = 11 scénarios).
- Les features v0.8+ des docs ne sont par ailleurs **pas committées** (untracked, cf. F-021).
**Proposition** : décider d'une source unique (Xray = master, export via `xray:export`), committer les exports versionnés (retirer `features/xray/` du .gitignore ou exporter vers un dossier versionné), et ajouter un step CI optionnel qui vérifie la présence des features pour la dernière version livrée.
**Effort** : M.

### F-017 — `AquaPlan.Worker` est un stub de template, en contradiction avec CLAUDE.md
**Fichiers** : `src/AquaPlan.Worker/Worker.cs` (boucle `Task.Delay(1000)` + log), `Program.cs` (host minimal).
**Constat** : CLAUDE.md décrit le Worker comme « *Background job runner (LIMS sync, notifications)* ». En réalité : aucun code métier, aucune référence aux autres projets, pas de Dockerfile, absent de docker-compose (dev et Synology), absent du pipeline docker, aucun projet de test. La sync LIMS tourne en fait dans l'API (`LimsSync` dans appsettings + `MockLims`).
**Proposition** : soit supprimer le projet (YAGNI — recommandé tant que Limsophy réel n'est pas branché), soit l'amender dans CLAUDE.md (« placeholder, non déployé »). Décision à acter pour éviter qu'un agent y implémente des features fantômes.
**Effort** : XS (doc) / S (suppression propre du slnx).

### F-018 — Migrations EF : SQL brut répandu et rollback destructeur
**Fichiers** : 9 migrations contiennent `migrationBuilder.Sql(...)` (`RestructureStatusWorkflow`, `BackfillOrdersToAnalysisPrograms`, `FixBarcodeValidation`, `RemoveValidatedStatus`, …).
**Constat** : `20260416195905_BackfillOrdersToAnalysisPrograms.Down()` **recrée la table `order_analysis_profiles` vide** — un rollback perd silencieusement les liens commandes↔profils. `RemoveValidatedStatus` et `RemoveLatLng` droppent des colonnes/états sans chemin de retour des données. Aucune stratégie de backup documentée avant migration, et la migration s'exécute automatiquement au boot (F-019).
**Proposition** : documenter le caractère irréversible dans le XML-doc des migrations concernées ; ajouter un dump `pg_dump` automatique avant `docker-compose up` côté Synology (le script update.sh est l'endroit naturel) ; tester les `Down()` critiques ou les marquer `throw new NotSupportedException`.
**Effort** : M.

### F-019 — `MigrateAsync()` au démarrage : incompatible avec la cible K8S multi-replicas
**Fichier** : `src/AquaPlan.Api/Program.cs:149-153`
**Constat** : la migration auto au boot est confortable en single-instance Synology, mais CLAUDE.md annonce « K8S (prod target on Exoscale) ». Avec ≥2 replicas, deux pods peuvent exécuter les migrations en concurrence (lock Postgres partiel seulement, risque d'échec/corruption d'`__EFMigrationsHistory`).
**Proposition** : prévoir dès maintenant un job de migration séparé (init-container / `dotnet ef database update` dans un Job K8S) activable par flag (`Database__MigrateOnStartup=false`).
**Effort** : M.

### F-020 — 60 fichiers untracked, dont toute la documentation PO/QA v0.8→v0.93
**Constat** : `git status` montre 60 entrées `??` : `docs/po/v0.8…v0.93-*.md`, `docs/tests/gherkin/v0.8|v0.9|v0.91/**`, `docs/design/**`, `AquaPlan-prototype.html`, scripts de seed. La règle du projet (« *code not on Bitbucket is invisible* », étape 3 du Version Lifecycle) est violée pour ~5 versions de documentation : une perte du poste = perte de tout l'historique PO/QA récent.
**Proposition** : trier (les doublons « * 2 » → poubelle, cf. F-027), committer le reste en un commit `docs:` avec clés AQ, pousser.
**Effort** : S.

### F-021 — Pipeline Bitbucket : variables Docker Hub et tags `latest` mutables, pas de provenance
**Fichiers** : `bitbucket-pipelines.yml:70`, `.github/workflows/ci.yml:121-158`
**Constat** : les deux pipelines poussent `:latest` + `:shortsha` sur un compte Docker Hub **public personnel** (`francoischarriere/aquaplan-*`). Une application cantonale (santé publique) est déployée depuis un registre public personnel, sans signature ni digest pinning ; le NAS auto-pull `latest` (« auto-pulled by Synology scheduled task ») → quiconque compromet ce compte Docker Hub compromet la prod.
**Proposition** : passer sur un registre privé (GitHub Container Registry du repo, gratuit) ; déployer par digest ou tag immuable plutôt que `latest` ; activer 2FA sur le compte registry.
**Effort** : M.

### F-022 — Pas de README.md à la racine du repo
**Constat** : il n'existe **aucun** README (ni racine, ni docs/). Le point d'entrée réel est CLAUDE.md (orienté agents IA, non versionné pour humains nouveaux venus) et PLAN-DEVELOPPEMENT.md (obsolète, cf. F-040). Pour un projet destiné à être repris par le Canton/des tiers, c'est un manque : pas d'instructions de build, de lancement (`docker-compose up`), de seed, ni d'architecture.
**Proposition** : créer un README.md court (description, prérequis, quickstart dev, liens CLAUDE.md/docs/), à faire vivre via le Definition of Done.
**Effort** : S.

---

## 4. Findings — Medium

### F-023 — `.dockerignore` n'exclut ni `docs/` ni `tests/` ni les `.md`
**Fichier** : `.dockerignore`
**Constat** : le Dockerfile API fait `COPY . .` → tout `docs/` (design system, features, prompts), `tests/` (e2e inclus, hors reports), `AquaPlan-prototype.html` (61 Ko ×2) entrent dans le contexte et dans le layer build. Image finale non affectée (multi-stage) mais : contexte plus lourd, cache invalidé par toute modification de doc, et risque de fuite si on debug le stage build.
**Proposition** : ajouter `docs/`, `tests/`, `*.md`, `AquaPlan-prototype*.html`, `bitbucket-pipelines.yml`, `.github/` au `.dockerignore` (la restauration .NET teste les `tests/*.csproj` ? non — le Dockerfile restaure `AquaPlan.slnx` qui inclut les projets de tests → soit garder `tests/AquaPlan.*/`, soit restaurer uniquement `src/AquaPlan.Api/AquaPlan.Api.csproj`).
**Effort** : S (attention au restore slnx).

### F-024 — `MockLims.Enabled: true` par défaut et aucun `appsettings.Production.json`
**Fichiers** : `src/AquaPlan.Api/appsettings.json:32-38`, `docker-compose.synology.yml` (ne surcharge pas MockLims).
**Constat** : l'instance « Production » Synology tourne avec le mock LIMS et la sync activée (5 min) car rien ne les désactive. Acceptable tant que Limsophy n'est pas branché, mais le jour du raccordement réel, ce défaut silencieux est un piège (données mock en prod).
**Proposition** : créer `appsettings.Production.json` (`MockLims.Enabled: false` à terme, Serilog Warning, etc.) et expliciter `MockLims__Enabled: "true"` dans le compose Synology tant que c'est voulu.
**Effort** : S.

### F-025 — compose dev ≠ compose Synology : divergences non documentées
**Fichiers** : `docker-compose.yml` vs `docker-compose.synology.yml`
**Constat** : `postgres:17` vs `postgres:17-alpine` ; DB nommée `aquaplan_dev` vs `aquaplan` ; port 5432 **exposé sur l'hôte** en dev (toute machine du LAN peut se connecter avec `aquaplan/aquaplan_dev`) ; web sur 80 vs 8880 ; `mem_limit` seulement en prod ; `restart: unless-stopped` seulement en prod ; pas d'`ASPNETCORE_ENVIRONMENT` en dev (Development implicite ? non — l'image tourne en Production par défaut !). Ce dernier point signifie que le compose dev tourne en mode Production (pas de Swagger).
**Proposition** : ajouter `ASPNETCORE_ENVIRONMENT: Development` au compose dev ; binder le port DB sur `127.0.0.1:5432:5432` ; documenter les écarts voulus en tête de chaque fichier.
**Effort** : S.

### F-026 — CORS hardcodé `http://localhost:4200` + `AllowCredentials`
**Fichier** : `src/AquaPlan.Api/Program.cs:100-109`
**Constat** : l'origine autorisée est codée en dur (pas configurable par environnement). En prod ça fonctionne par accident (même origine via le proxy nginx `/api/`), mais le jour où l'API est exposée sur un autre host, il faudra recompiler. `AllowAnyHeader/AnyMethod + AllowCredentials` sur une origine de dev est inutile en prod.
**Proposition** : `Cors__AllowedOrigins` en configuration, politique vide/désactivée en Production.
**Effort** : S.

### F-027 — 16+ doublons macOS « * 2.* » qui polluent le repo
**Constat** : `AquaPlan-prototype 2.html`, `docs/po/* 2.md` (×10), `scripts/seed_* 2.*`, `docs/tests/non-regression/AQ-194-post-v0.91-run 2.md`, `.claude/scheduled_tasks 2.lock`, et des dizaines dans `obj/` (`project.assets 2.json`…). Produits par une copie/synchro macOS (iCloud ?). Risque réel : éditer la mauvaise copie, committer du bruit, et les doublons `obj/` peuvent perturber MSBuild.
**Proposition** : `find . -name "* 2.*" -delete` après revue ; vérifier que le dossier projet n'est pas sous synchronisation iCloud Drive (cause classique) ; ajouter `* 2.*` au .gitignore en garde-fou.
**Effort** : XS.

### F-028 — `npm run report` cassé : `scripts/generate-report.mjs` n'existe pas
**Fichier** : `tests/e2e/package.json:15` → `"report": "node scripts/generate-report.mjs"` ; le dossier `tests/e2e/scripts/` ne contient que `xray-export.mjs` et `xray-import.mjs`.
**Proposition** : supprimer le script ou créer le générateur (le HTML report Cucumber existe déjà via `cucumber.mjs` → le script est probablement superflu).
**Effort** : XS.

### F-029 — Scripts xray-*.mjs : chargement de `../../.env` fragile et silencieux
**Fichiers** : `tests/e2e/scripts/xray-export.mjs:22`, `xray-import.mjs:22`
```js
config({ path: resolve(projectRoot, '../../.env') });
```
**Constat** : le chemin remonte de `tests/e2e` vers la racine — correct aujourd'hui, mais `dotenv` ne signale pas l'absence du fichier ; l'erreur apparaît plus tard (« XRAY_CLIENT_ID must be set »). Couplage direct des tests aux secrets racine (cf. F-002). Le token Xray transite aussi en GET param ? Non — header, OK.
**Proposition** : accepter aussi les variables d'environnement ambiantes (déjà le cas) et logguer le chemin .env résolu ; à terme, lire les secrets du trousseau.
**Effort** : XS.

### F-030 — `synology-update.sh` suppose un fichier `docker-compose.yml` sur le NAS ≠ nom du repo
**Fichier** : `scripts/synology-update.sh:34` (`docker-compose -f docker-compose.yml up -d`) alors que le repo livre `docker-compose.synology.yml` ; le pipeline Bitbucket parle de `sudo ./update.sh` (le script s'appelle `synology-update.sh` dans le repo).
**Constat** : la procédure implicite (copier `docker-compose.synology.yml` → `/volume1/docker/aquaplan/docker-compose.yml`, copier `synology-update.sh` → `update.sh`) n'est documentée nulle part. Toute reconstruction du NAS échouera sans connaissance tribale.
**Proposition** : README de déploiement (ou en-tête de script) décrivant les fichiers attendus sur le NAS, ou faire référencer `docker-compose.synology.yml` directement.
**Effort** : XS.

### F-031 — `seed_synology_docker.sh` : `set -e` seul, et en-tête « Canton de Vaud (SCAV) »
**Fichier** : `scripts/seed_synology_docker.sh`
**Constat** : (1) pas de `set -u`/`pipefail` (les deux autres scripts shell les ont) — un `$TOKEN` vide n'est intercepté que par un test manuel ; usage de `local` hors fonction serait fatal avec `set -u`. (2) Le commentaire dit « Canton de Vaud (SCAV) » et seed Lausanne/eauservice alors que le projet est Fribourg (SAAV) — données de seed incohérentes avec le contexte annoncé partout ailleurs.
**Proposition** : `set -eu` (pipefail n'existe pas en `sh` POSIX — passer le shebang en bash si besoin) ; clarifier l'usage Vaud (démo multi-tenant ?) ou corriger.
**Effort** : XS.

### F-032 — `bitbucket-pipelines.yml` encore présent et actif-ambigu après migration
**Constat** : le fichier reste à la racine sans marqueur de dépréciation ; si le repo Bitbucket reste le `origin` poussé (c'est le cas), **les deux pipelines tournent en parallèle** et poussent tous deux `:latest` sur Docker Hub — la dernière exécution gagne, avec des images potentiellement différentes (le build web Bitbucket ne passe pas `APP_VERSION`, cf. F-012 — versions affichées divergentes selon le pipeline qui a gagné).
**Proposition** : décider du remote canonique ; désactiver les pipelines Bitbucket (Repository settings → Pipelines off) ou supprimer le fichier avec une note de migration ; au minimum ajouter un commentaire « DEPRECATED — voir .github/workflows/ci.yml ».
**Effort** : XS.

### F-033 — Cache npm : régression partielle Bitbucket → GitHub
**Constat** : Bitbucket cachait `node_modules` (`caches: npm: src/AquaPlan.Web/ClientApp/node_modules`) ; GitHub Actions cache `~/.npm` via setup-node (`cache: 'npm'`) — c'est la bonne pratique (npm ci supprime node_modules de toute façon) mais le `npm ci` complet reste exécuté à chaque run (~minutes). Le job e2e n'existe dans aucun des deux pipelines : `tests/e2e` n'a **jamais** tourné en CI alors que docker-compose a un profil `e2e` prêt.
**Proposition** : acceptable en l'état pour npm ; ajouter un job CI optionnel (manuel/nightly) qui lance `docker compose --profile e2e up --abort-on-container-exit` pour exécuter au moins smoke+auth.
**Effort** : M (job e2e).

### F-034 — Pas de scan de vulnérabilités ni de lint dans les pipelines
**Constat** : aucun `npm audit`, `dotnet list package --vulnerable`, scan d'images (Trivy/Grype), ni lint Angular (`ng lint`) dans bitbucket-pipelines.yml ou ci.yml. Pour une application du domaine santé/environnement, un scan d'images avant push registre est un minimum.
**Proposition** : ajouter un job Trivy sur les deux images + `dotnet list package --vulnerable --include-transitive` en step non bloquant d'abord.
**Effort** : S.

### F-035 — nginx : pas de `server_tokens off`, pas de HSTS, `X-XSS-Protection` obsolète
**Fichier** : `src/AquaPlan.Web/nginx.conf`
**Constat** : la version nginx est exposée dans les headers/pages d'erreur ; aucun `Strict-Transport-Security` (le TLS est terminé par le reverse proxy DSM — le header peut quand même être émis ici puisque le 301 HTTPS existe) ; `X-XSS-Protection` est déprécié et peut introduire des comportements indésirables sur vieux navigateurs.
**Proposition** : `server_tokens off;`, ajouter `add_header Strict-Transport-Security "max-age=31536000" always;` (appliqué seulement aux réponses via HTTPS/X-Forwarded-Proto=https idéalement), supprimer X-XSS-Protection.
**Effort** : XS.

### F-036 — Healthcheck absent pour les conteneurs api/web (hors DB)
**Fichiers** : `docker-compose.yml`, `docker-compose.synology.yml`
**Constat** : seule la DB a un healthcheck. `aquaplan-web` démarre dès que `aquaplan-api` est *créé* (pas healthy) ; le NAS ne redémarre pas un conteneur API zombie (process up mais DB inaccessible). L'API expose pourtant `/api/health`.
**Proposition** : ajouter `healthcheck: curl -f http://localhost:8080/api/health` sur l'API (+`depends_on: condition: service_healthy` côté web) — attention : l'image alpine n'a pas curl → utiliser `wget -q -O-` (busybox) ou un HealthCheck .NET.
**Effort** : S.

---

## 5. Findings — Low

### F-037 — TODO actifs dans le code livré
**Fichiers** : `src/AquaPlan.Web/ClientApp/src/app/services/sync.service.ts:243,253` (TODO AQ-376 — replace-location offline non branché), `pages/sampling-rounds/sampling-round-detail.component.ts:507`.
**Constat** : 3 TODO seulement (codebase propre), mais AQ-376 correspond à une feature v0.91 « terminée » — le TODO indique un reste à faire non tracké.
**Proposition** : vérifier le statut Jira d'AQ-376 et créer un ticket de suivi si le plumbing offline n'est pas branché.
**Effort** : XS.

### F-038 — `AquaPlan-prototype.html` (61 Ko) à la racine, untracked, en double
**Proposition** : déplacer vers `docs/design/` (ou supprimer — le design system AQ-424 l'a remplacé) et committer ou ignorer explicitement.
**Effort** : XS.

### F-039 — Trois scripts de seed redondants (2 Python + 1 sh) sans factorisation
**Fichiers** : `scripts/seed_data.py` (localhost), `seed_data_synology.py` (NAS), `seed_synology_docker.sh` (dans le conteneur).
**Constat** : trois implémentations parallèles de la même logique avec des jeux de données divergents ; le shell est le moins lisible (parsing JSON au `sed`). Python + paramètre `--api-url` couvrirait les trois cas.
**Proposition** : fusionner en un seul `seed_data.py --target localhost|synology|container` ; garder le sh seulement si l'exécution in-container sans Python est indispensable.
**Effort** : S.

### F-040 — `PLAN-DEVELOPPEMENT.md` et `docs/plan-integration-tests.md` obsolètes
**Constat** : PLAN-DEVELOPPEMENT.md référence `AquaPlan.sln` (le repo utilise `AquaPlan.slnx`), un planning « 4 releases » dépassé par la réalité (v0.94 livrée), et s'appuie sur des liens Confluence externes (la connaissance n'est pas dans le repo). `plan-integration-tests.md` décrit un état du 2026-03-30 (« 3 commits locaux, 44 fichiers non committés ») sans valeur actuelle. CLAUDE.md affirme aussi « Solution file: `AquaPlan.sln` » → incohérent avec `AquaPlan.slnx`.
**Proposition** : archiver les deux fichiers dans `docs/archive/` avec un bandeau « historique » ; corriger CLAUDE.md (`AquaPlan.slnx`).
**Effort** : XS.

### F-041 — `.claude/launch.json` et agents committés référencent Bitbucket (`francisuster/aquaplan`)
**Fichiers** : `.claude/agents/jira-updater.md:404` (API Bitbucket), `docs/design/project/README.md` (« branch main » alors que la branche est `Main`).
**Constat** : après migration GitHub, les agents continueront à pousser/chercher les PR sur Bitbucket. Incohérences de casse de branche (`main` vs `Main`) dans les docs design.
**Proposition** : mettre à jour les agents au moment de la bascule définitive (lié à F-032).
**Effort** : S.

### F-042 — Pas de Dependabot/Renovate
**Constat** : ni `.github/dependabot.yml` ni renovate.json — actions GitHub épinglées sur tags majeurs (@v4), images de base non surveillées (postgres:17, node:22-alpine, dotnet:10.0-alpine, playwright pinné v1.52.0).
**Proposition** : dependabot.yml minimal (github-actions, npm ClientApp + e2e, nuget, docker).
**Effort** : S.

### F-043 — cucumber.mjs : exécution séquentielle, pas de retry, timeout 30 s global
**Fichiers** : `tests/e2e/cucumber.mjs` (`parallel: 1`), `support/world.ts` (`setDefaultTimeout(30_000)`).
**Constat** : pour ~116 scénarios, l'exécution séquentielle sera longue (la règle projet évoque déjà « 60+ tests, can be long ») ; aucun retry sur les scénarios UI flaky.
**Proposition** : `parallel: 2-4` une fois les step-defs réelles écrites (l'état partagé `sharedBrowser` devra devenir per-worker), `retry: 1` pour les tags `@ui`.
**Effort** : S.

---

## 6. Migration Bitbucket → GitHub Actions : items à corriger

Comparaison ligne à ligne `bitbucket-pipelines.yml` → `.github/workflows/ci.yml` :

| # | Item | Bitbucket | GitHub | Verdict |
|---|---|---|---|---|
| 1 | Build+test .NET (Release, trx, coverage) | ✅ | ✅ identique | OK |
| 2 | Build Angular prod + generate-version | ✅ | ✅ (ordre npm ci/generate inversé, sans impact) | OK |
| 3 | Artifacts test/dist | ✅ | ✅ (+ retention 14 j) | OK, amélioré |
| 4 | **CI sur toutes les branches** | ✅ (`default:`) | ❌ Main + PR→Main uniquement | **À corriger (F-011)** |
| 5 | Docker build/push API+Web sur Main | ✅ | ✅ + buildcache registry (mieux) | OK, amélioré |
| 6 | `APP_VERSION` du build web | ❌ absent (fallback GitVersion.yml in-image) | ⚠️ hardcodé `0.94.0` | **À corriger (F-12) — divergence 0.93/0.94 selon pipeline** |
| 7 | Step « Tag for Deployment » (marqueur `deployment: staging`) | ✅ | ❌ aucun équivalent (environnements GitHub non utilisés) | Mineur — recréer un `environment: staging` si la traçabilité de déploiement compte |
| 8 | Timeouts | ❌ (défaut 120 min) | ✅ 20/30 min | Amélioré |
| 9 | `permissions:` / `concurrency:` | n/a | ❌ absents | **À ajouter (F-013)** |
| 10 | Secrets | `$DOCKERHUB_*` variables workspace (secured à vérifier dans l'UI Bitbucket) | `secrets.DOCKERHUB_*` | OK des deux côtés (rien en clair dans les YAML) |
| 11 | Plateformes images | implicite amd64 | explicite `linux/amd64` | OK (Synology DS218+ est aarch64 ?? — DS218+ est un Celeron x64 → amd64 correct) |
| 12 | Tests e2e | ❌ jamais présents | ❌ toujours absents | Opportunité (F-033) |
| 13 | Double exécution | — | ⚠️ les **deux** pipelines restent actifs si on pousse sur les deux remotes → courses sur `:latest` | **À trancher (F-032)** |

**Conclusion migration** : le port est fidèle et même meilleur (timeouts, cache registry, retention). Les 4 vrais points à corriger : couverture de branches (#4), APP_VERSION hardcodé (#6), permissions/concurrency (#9), et la cohabitation des deux pipelines (#13).

---

## 7. État réel de `tests/e2e` : que faut-il pour les rendre opérationnels

**Ce qui marche aujourd'hui (vérifié pendant l'audit)** :
- `npx cucumber-js --dry-run` passe : **116 scénarios / 384 steps, 0 step indéfini** (sur ce poste, avec les 43 features Xray exportées localement).
- Le tooling est sain : Playwright 1.52 + Cucumber 11 + tsx, hooks screenshots-on-failure, world API/UI propre, Dockerfile e2e fonctionnel, profil compose `e2e` prêt, scripts Xray export/import corrects (auth, GraphQL, mapping @AQ-xxx → testKey, statut TO DO géré).

**Mais l'absence de steps indéfinis est un trompe-l'œil** :
- **0 step orphelin « bloquant », car `catchall.steps.ts` (737 lignes) absorbe tout** : 66 de ses ~120 steps se terminent par `expect(true).toBeTruthy()` (+24 stubs dans frontend/business-api/auth-roles). Environ **un quart des 384 steps exécutent une assertion vide** → les scénarios « passent » sans rien vérifier (F-014).
- **Tous les rôles = admin** (F-015) : les scénarios RBAC/tenant ne testent rien.
- **Reproductibilité nulle sur un clone frais** : `features/xray/` est gitignoré → il ne reste que 3 features (11 scénarios). Les 46 features Gherkin v0.8/v0.9/v0.91 de `docs/tests/gherkin/` n'ont **aucune** contrepartie e2e (clés AQ-303..379 absentes, recouvrement avec AQ-128..190 : zéro) (F-016).
- `npm run report` cassé (F-028) ; dépendance à `.env` racine et à ses secrets (F-029) ; jamais branchés en CI (F-033).

**Plan de remise en service (ordre conseillé)** :
1. *(S)* Committer les features Xray exportées (lever le gitignore) — reproductibilité.
2. *(S)* Convertir les stubs `expect(true)` en `'pending'` → les exécutions Xray reflètent la réalité (TO DO au lieu de PASSED).
3. *(M)* Seeder des comptes par rôle + corriger `credentials.ts`.
4. *(M)* Exporter/implémenter les features v0.9/v0.91 (AQ-333..379) : ce sont les versions actives.
5. *(M)* Job CI nightly `docker compose --profile e2e` sur smoke+auth (déjà implémentés pour de vrai).
6. *(L→XL)* Remplacer progressivement les catchall par de vraies assertions, en commençant par RBAC/tenant.

---

## 8. Documentation : ce qui est obsolète ou divergent

| Document | État | Détail |
|---|---|---|
| README racine | **Inexistant** | Aucun point d'entrée humain (F-022). |
| `CLAUDE.md` | Divergent (3 points) | (1) « Solution file: `AquaPlan.sln` » → c'est `AquaPlan.slnx`. (2) Worker décrit comme « LIMS sync, notifications » → stub vide (F-017). (3) La règle « tests xUnit obligatoires » est, elle, **respectée** sur les livraisons récentes vérifiées : AQ-407/415/416 et AQ-43/44/45 ont bien `ResultsControllerTest`, `ResultsServiceTest`, `MockLimsBackfillServiceTest`, `NotificationsControllerTest`, `NotificationServiceTest`. ✔ |
| `PLAN-DEVELOPPEMENT.md` | Obsolète | Planning initial 4 releases, références `.sln`, contenu réel sur Confluence externe (F-040). |
| `docs/plan-integration-tests.md` | Obsolète | Photographie du 2026-03-30 (« 3 commits locaux »). À archiver. |
| `docs/po/*` (v0.8→v0.93) | **Non committé** | 12 fichiers + 10 doublons « 2 » untracked — la traçabilité PO des 5 dernières versions n'existe que sur ce poste (F-020, F-027). |
| `docs/tests/gherkin/` | Committé jusqu'à v0.2 seulement | v0.8/v0.9/v0.91 untracked ; v0.92/v0.93/v0.94 absents (pas de features du tout pour les 3 dernières versions). |
| `docs/tests/non-regression/AQ-194-post-v0.91-run.md` | Untracked | Le run de non-régression v0.91 n'est pas versionné. |
| `docs/po/*-jira-mapping.md` vs Jira | Non vérifiable hors ligne | Les mappings v0.8→v0.92 référencent des clés AQ-303..379 cohérentes avec les features Gherkin du même dossier (cohérence interne OK) ; la cohérence avec Jira Cloud est à re-valider après commit. |
| `docs/design/**` | Untracked | Bundle handoff Claude Design (AQ-424) non versionné ; son README mentionne « branche `main` » (la branche est `Main`) et un accès Bitbucket. |
| `bitbucket-pipelines.yml` | Ambigu | Toujours actif si pushes vers Bitbucket continuent ; aucun marqueur de dépréciation (F-032). |
| `GitVersion.yml` | Retard | `next-version: 0.93.0` alors que v0.94 est livrée (F-012). |
| Doc de déploiement Synology | Tribale | Renommages implicites compose/update.sh non documentés (F-030). |

---

## 9. Récapitulatif des findings

| ID | Sévérité | Titre | Effort |
|---|---|---|---|
| F-001 | Critical | Token Jira committé dans `.claude/settings.local.json` (poussé) | S+M |
| F-002 | Critical | `.env` : 4 credentials actifs en clair | S |
| F-003 | Critical | PAT GitHub dans l'URL du remote git | XS |
| F-004 | Critical | Secrets prod en fallback dans docker-compose.synology.yml | S |
| F-005 | Critical | Admin `Admin123!` seedé en dur, utilisé en prod | M |
| F-006 | Critical | Security headers nginx non appliqués aux documents HTML | S |
| F-007 | Critical | Conteneur API en root, `COPY . .` | S/M |
| F-008 | High | CSP laxiste (unsafe-eval, connect-src https:) | S |
| F-009 | High | deploy-synology.sh ne déploie jamais la nouvelle image | S |
| F-010 | High | sudo docker via SSH, accept-new | S |
| F-011 | High | CI GitHub absente hors Main | XS |
| F-012 | High | Versions divergentes 0.94/0.93/0.0.0 | S |
| F-013 | High | ci.yml sans permissions/concurrency | XS |
| F-014 | High | ~90 step-defs e2e auto-PASS → Xray faussé | S/XL |
| F-015 | High | Tous les rôles e2e = compte admin | M |
| F-016 | High | Features Gherkin désynchronisées, features/xray gitignoré | M |
| F-017 | High | Worker = stub, contredit CLAUDE.md | XS/S |
| F-018 | High | Migrations EF : SQL brut, Down destructif | M |
| F-019 | High | MigrateAsync au boot vs cible K8S | M |
| F-020 | High | 60 fichiers untracked (docs PO/QA v0.8→v0.93) | S |
| F-021 | High | Registre Docker Hub public personnel + latest mutable | M |
| F-022 | High | Pas de README racine | S |
| F-023 | Medium | .dockerignore n'exclut pas docs/tests | S |
| F-024 | Medium | MockLims actif par défaut, pas d'appsettings.Production | S |
| F-025 | Medium | compose dev ≠ prod (dont env Development manquant) | S |
| F-026 | Medium | CORS hardcodé localhost:4200 | S |
| F-027 | Medium | Doublons macOS « * 2.* » | XS |
| F-028 | Medium | npm run report cassé | XS |
| F-029 | Medium | xray-*.mjs : chargement .env fragile | XS |
| F-030 | Medium | Renommages implicites compose/update.sh sur le NAS | XS |
| F-031 | Medium | seed_synology_docker.sh : set -e seul + en-tête « Vaud » | XS |
| F-032 | Medium | bitbucket-pipelines.yml encore actif/ambigu | XS |
| F-033 | Medium | e2e jamais en CI ; cache npm OK | M |
| F-034 | Medium | Pas de scan vulnérabilités/lint en CI | S |
| F-035 | Medium | nginx : server_tokens, HSTS, X-XSS-Protection | XS |
| F-036 | Medium | Pas de healthcheck api/web | S |
| F-037 | Low | TODO AQ-376 actifs | XS |
| F-038 | Low | AquaPlan-prototype.html à la racine | XS |
| F-039 | Low | 3 scripts de seed redondants | S |
| F-040 | Low | PLAN-DEVELOPPEMENT / plan-integration-tests obsolètes | XS |
| F-041 | Low | Agents .claude pointent encore Bitbucket | S |
| F-042 | Low | Pas de Dependabot/Renovate | S |
| F-043 | Low | cucumber séquentiel, sans retry | S |

---

*Audit réalisé en lecture seule le 2026-06-11. Vérifications dynamiques limitées à `git` (historique, tracked files) et `cucumber-js --dry-run`. Aucune modification apportée au code, à la configuration ou aux remotes.*
