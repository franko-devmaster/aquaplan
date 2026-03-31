# Prompt Claude Code — Développement AquaPlan v0.2 (Version définitive)

> Copier-coller le contenu entre les triple backticks dans Claude Code pour lancer le développement autonome de la v0.2.

---

## Le prompt

```
Tu es le développeur principal du projet AquaPlan — une application web de gestion de la qualité de l'eau potable pour le Canton de Fribourg (Suisse).

## Stack technique
- Backend : .NET 10, Clean Architecture (Domain / Application / Infrastructure / Api / Worker / Shared)
- Frontend : Angular 19, Angular Material, Bootstrap, i18n FR/DE
- Base de données : PostgreSQL + Entity Framework Core
- Auth : JWT + OpenID Connect (EntraID / Microsoft Entra ID)
- CI/CD : Bitbucket Pipelines
- Conteneurisation : Docker / docker-compose
- Tests : xUnit (.NET), Jasmine/Karma (Angular), Gherkin BDD (Xray)
- Hébergement : Exoscale (SKS Kubernetes)
- LIMS : Limsophy (intégration future)

## Jira & Xray
- Projet Jira Cloud : AQ sur https://chfr.atlassian.net
- Xray Cloud pour la gestion des tests (API : xray.cloud.getxray.app)
- Credentials dans .env :
  - JIRA_USER_EMAIL (email pour auth Basic Jira REST API)
  - JIRA_API_TOKEN (API token Jira)
  - XRAY_CLIENT_ID (81900D430DD346A1B961F80BE4AB0F56)
  - XRAY_CLIENT_SECRET (dans .env)
- Agent `.claude/agents/xray-tester.md` pour les opérations Xray
- Agent `.claude/agents/jira-updater.md` pour les mises à jour Jira

## Authentification API Xray Cloud

```bash
# Obtenir un token JWT Xray
source .env
XRAY_TOKEN=$(curl -s -X POST "https://xray.cloud.getxray.app/api/v2/authenticate" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$XRAY_CLIENT_ID\",\"client_secret\":\"$XRAY_CLIENT_SECRET\"}" \
  | tr -d '"')
```

## Authentification API Jira Cloud

```bash
source .env
# Utiliser Basic Auth : $JIRA_USER_EMAIL:$JIRA_API_TOKEN
# Endpoint JQL (API v3 actuelle) :
curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
  -G "https://chfr.atlassian.net/rest/api/3/search/jql" \
  --data-urlencode "jql=project=AQ AND key=AQ-24" \
  -H "Accept: application/json"
```

## État actuel du projet (au 30 mars 2026)

### v0.1 — Infrastructure : ✅ TERMINÉE
Toutes les stories AQ-70 à AQ-77 sont terminées. Le scaffolding est complet :
- Solution .NET avec 7 projets (Api, Application, Domain, Infrastructure, Shared, Web, Worker)
- DbContext EF Core configuré avec 10 entity configurations
- Docker-compose et Bitbucket Pipelines en place
- Angular 19 ClientApp initialisé dans AquaPlan.Web
- Tests unitaires existants pour Controllers et Middleware

### v0.2 — Auth & Rôles : EN COURS
Statut des stories au 30 mars 2026 :

| Story | Titre | Statut Jira | Action requise |
|-------|-------|-------------|----------------|
| AQ-49 | Permissions via 4 rôles prédéfinis | ✅ Terminé | Vérifier l'implémentation |
| AQ-50 | Rôle Requérant | ✅ Terminé | Vérifier l'implémentation |
| AQ-51 | Rôle Préleveur | ✅ Terminé | Vérifier l'implémentation |
| AQ-52 | Rôle Requérant-Préleveur | ✅ Terminé | Vérifier l'implémentation |
| AQ-53 | Rôle Administrateur | ✅ Terminé | Vérifier l'implémentation |
| AQ-54 | Affectation/retrait de rôles + audit trail | ✅ Terminé | Vérifier l'implémentation |
| AQ-81 | Création de compte utilisateur | ✅ Terminé | Vérifier l'implémentation |
| AQ-82 | Modification de compte utilisateur | ✅ Terminé | Vérifier l'implémentation |
| AQ-83 | Désactivation de compte utilisateur | ✅ Terminé | Vérifier l'implémentation |
| AQ-24 | Authentification via compte IdP (OIDC) | 🔄 En cours | **IMPLÉMENTER** |
| AQ-25 | SSO via EntraID | 🔄 En cours | **IMPLÉMENTER** |
| AQ-55 | Visibilité restreinte LDP par réseau | 🔄 En cours | **IMPLÉMENTER** |
| AQ-56 | Accès mandataire limité à ses communes | 🔄 En cours | **IMPLÉMENTER** |
| AQ-57 | Accès préleveur limité à ses mandats | 🔄 En cours | **IMPLÉMENTER** |

### Code existant (à connaître et utiliser)
```
src/
├── AquaPlan.Api/
│   ├── Controllers/Api/
│   │   ├── AuthController.cs
│   │   ├── HealthController.cs
│   │   ├── LogsController.cs
│   │   ├── OrdersController.cs
│   │   ├── RolesController.cs
│   │   ├── SamplingLocationsController.cs
│   │   └── UsersController.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── ApiExceptionFilterAttribute.cs
│   └── Program.cs
├── AquaPlan.Application/
│   ├── DTOs/
│   │   ├── Auth/ (LoginDto, LoginResponseDto, RefreshTokenDto, UserInfoDto)
│   │   ├── Orders/ (CreateOrderDto, OrderDetailDto, OrderListDto, AssignOrderDto, SamplingDto)
│   │   ├── Roles/ (RoleDto, RoleAssignDto, RoleWithPermissionsDto)
│   │   ├── SamplingLocations/ (CreateSamplingLocationDto, SamplingLocationDto, UpdateSamplingLocationDto)
│   │   ├── Samplings/ (CreateSamplingDto, ValidateSamplingDto)
│   │   ├── Users/ (CreateUserDto, UserDetailDto, UserListDto, UpdateUserDto)
│   │   └── Logging/ (ClientLogDto)
│   └── Services/Interfaces/
│       ├── IAuthService.cs
│       ├── IOrderService.cs
│       ├── IPermissionService.cs
│       ├── ISamplingLocationService.cs
│       ├── ITokenService.cs
│       └── IUserManagementService.cs
├── AquaPlan.Domain/
│   ├── Entities/
│   │   ├── AppUser.cs, ApplicationRole.cs, Permission.cs, RolePermission.cs
│   │   ├── Tenant.cs, Distributor.cs, UserDistributor.cs
│   │   ├── Order.cs, Sampling.cs, SamplingLocation.cs
│   └── Enums/
│       ├── OrderStatus.cs, PermissionName.cs, RoleName.cs
├── AquaPlan.Infrastructure/
│   ├── Data/
│   │   ├── AquaPlanDbContext.cs
│   │   ├── Configurations/ (9 entity configs)
│   │   └── RoleAndPermissionSeeder.cs
│   ├── Services/
│   │   ├── OrderService.cs, PermissionService.cs
│   │   ├── SamplingLocationService.cs, UserManagementService.cs
│   └── Security/
│       └── TokenService.cs
├── AquaPlan.Web/
│   └── ClientApp/ (Angular 19)
├── AquaPlan.Worker/
└── AquaPlan.Shared/

tests/
├── AquaPlan.Api.Tests/
│   ├── Controllers/ (Auth, Health, Orders, Roles, SamplingLocations, Users)
│   └── Middleware/ (CorrelationIdMiddleware)
├── AquaPlan.Application.Tests/
├── AquaPlan.Infrastructure.Tests/
└── AquaPlan.Tests.Shared/
```

### Entités Domain clés
- **AppUser** : Id, Email, FirstName, LastName, ExternalId (IdP), IsActive, TenantId, Distributors
- **ApplicationRole** : Id, Name (enum RoleName), RolePermissions
- **Permission** : Id, Name (enum PermissionName)
- **Distributor** : représente un réseau de distribution d'eau (commune/intercommunale)
- **UserDistributor** : liaison M:N User↔Distributor (accès réseau)
- **Order** : mandat de prélèvement, Status (enum OrderStatus), AssignedTo (préleveur)
- **SamplingLocation** : lieu de prélèvement (LDP), lié à un Distributor
- **Sampling** : résultat de prélèvement, lié à Order + SamplingLocation

### Enums existants
- **RoleName** : Requerant, Preleveur, RequerantPreleveur, Administrateur
- **PermissionName** : à vérifier dans le code
- **OrderStatus** : à vérifier dans le code

## Scénarios Gherkin (critères d'acceptation)
Les fichiers .feature sont dans `docs/tests/gherkin/v0.2/` :
- AQ-24.feature, AQ-25.feature
- AQ-49.feature à AQ-57.feature
- AQ-81.feature, AQ-82.feature, AQ-83.feature

**IMPORTANT** : Lis chaque fichier .feature AVANT de coder la story correspondante. Les scénarios Gherkin définissent les critères d'acceptation exacts.

## Mapping Tests Xray → Stories
Les tests Xray existent déjà dans Jira avec leur Gherkin importé :

### v0.2 Auth & Rôles (Test Plan AQ-151)
| Test Xray | Story | Xray Internal ID |
|-----------|-------|-------------------|
| AQ-136 | AQ-24 | 10843 |
| AQ-137 | AQ-25 | 10844 |
| AQ-138 | AQ-49 | 10845 |
| AQ-139 | AQ-50 | 10846 |
| AQ-140 | AQ-51 | 10847 |
| AQ-141 | AQ-52 | 10848 |
| AQ-142 | AQ-53 | 10849 |
| AQ-143 | AQ-54 | 10850 |
| AQ-144 | AQ-55 | 10851 |
| AQ-145 | AQ-56 | 10852 |
| AQ-146 | AQ-57 | 10853 |
| AQ-147 | AQ-81 | 10854 |
| AQ-148 | AQ-82 | 10855 |
| AQ-149 | AQ-83 | 10856 |

### Test Executions existantes
- AQ-152 : v0.1 Infrastructure
- AQ-153 : v0.2 Auth & Rôles
- AQ-154 : Exécution automatisée 2026-03-30 (22 tests)

## Workflow de développement — Pour chaque Story "En cours"

### Étape 1 : Lire et comprendre
1. Lis `docs/tests/gherkin/v0.2/AQ-XX.feature` pour les critères d'acceptation
2. Lis le code existant lié (controllers, services, DTOs, entities)
3. Identifie ce qui manque vs ce qui existe déjà

### Étape 2 : Coder (Clean Architecture)
- **Domain** : entités, interfaces de repository, value objects, enums
- **Application** : interfaces de services, DTOs, mapping, validation
- **Infrastructure** : implémentation EF Core (repos, services), migrations, configs
- **Api** : controllers RESTful, middleware, authorization policies
- **Web/ClientApp** : composants Angular (si frontend concerné)

### Étape 3 : Tests unitaires
- Ajoute des tests xUnit dans `tests/` pour chaque nouveau service/controller
- Pattern : Arrange / Act / Assert
- Utilise Moq pour les mocks de dépendances

### Étape 4 : Build & Test
```bash
dotnet build AquaPlan.slnx
dotnet test AquaPlan.slnx
```

### Étape 5 : Commit & Push
```bash
git add -A
git commit -m "feat(AQ-XX): description courte de ce qui a été implémenté"
git push origin main
```

### Étape 6 : Importer résultat dans Xray
```bash
source .env
XRAY_TOKEN=$(curl -s -X POST "https://xray.cloud.getxray.app/api/v2/authenticate" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$XRAY_CLIENT_ID\",\"client_secret\":\"$XRAY_CLIENT_SECRET\"}" \
  | tr -d '"')

# Importer le résultat du test correspondant
curl -s -X POST "https://xray.cloud.getxray.app/api/v2/import/execution" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $XRAY_TOKEN" \
  -d '{
    "testExecutionKey": "AQ-153",
    "tests": [
      {"testKey": "AQ-XXX", "status": "PASSED", "comment": "Implémentation validée — tests unitaires OK"}
    ]
  }'
```
Statuts Xray disponibles : PASSED, FAILED, TO DO, EXECUTING

### Étape 7 : Transition Jira → Terminé
```bash
source .env
# 1. Récupérer les transitions disponibles
TRANSITIONS=$(curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
  "https://chfr.atlassian.net/rest/api/3/issue/AQ-XX/transitions")
echo $TRANSITIONS | python3 -m json.tool

# 2. Trouver l'ID de la transition "Terminé" / "Done" et l'appliquer
TRANSITION_ID=<id_trouvé>
curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
  -X POST "https://chfr.atlassian.net/rest/api/3/issue/AQ-XX/transitions" \
  -H "Content-Type: application/json" \
  -d "{\"transition\":{\"id\":\"$TRANSITION_ID\"}}"
```

## Stories à implémenter (dans cet ordre)

### 1. AQ-24 — Authentification via compte IdP (OpenID Connect)
**Gherkin** : `docs/tests/gherkin/v0.2/AQ-24.feature`
**Test Xray** : AQ-136
**À faire** :
- Configurer OpenID Connect dans Program.cs (Microsoft.Identity.Web ou similaire)
- Ajouter les settings OIDC dans appsettings.json (Authority, ClientId, etc.)
- Implémenter le flow de login/callback dans AuthController
- Mapper le claim `sub` vers AppUser.ExternalId
- Créer automatiquement l'AppUser au premier login si inexistant
- Ajouter les tests unitaires

### 2. AQ-25 — SSO via EntraID (Microsoft Entra ID)
**Gherkin** : `docs/tests/gherkin/v0.2/AQ-25.feature`
**Test Xray** : AQ-137
**À faire** :
- Configurer Microsoft Entra ID comme IdP spécifique
- Ajouter les paramètres tenant/authority EntraID dans appsettings
- Supporter le SSO silencieux (si session active)
- Gérer le logout (revocation du token + redirect)
- Ajouter les tests unitaires

### 3. AQ-55 — Visibilité restreinte LDP par réseau
**Gherkin** : `docs/tests/gherkin/v0.2/AQ-55.feature`
**Test Xray** : AQ-144
**À faire** :
- Filtrer les SamplingLocations visibles en fonction des Distributors de l'utilisateur
- Modifier SamplingLocationService pour appliquer ce filtre
- Modifier le controller pour injecter le contexte utilisateur
- L'administrateur voit tous les LDP
- Ajouter les tests unitaires

### 4. AQ-56 — Accès mandataire limité à ses communes
**Gherkin** : `docs/tests/gherkin/v0.2/AQ-56.feature`
**Test Xray** : AQ-145
**À faire** :
- Filtrer les Orders visibles : un Requérant ne voit que les mandats de ses Distributors
- Modifier OrderService pour appliquer ce filtre
- L'administrateur voit tout
- Ajouter les tests unitaires

### 5. AQ-57 — Accès préleveur limité à ses mandats
**Gherkin** : `docs/tests/gherkin/v0.2/AQ-57.feature`
**Test Xray** : AQ-146
**À faire** :
- Un Préleveur ne voit que les mandats qui lui sont assignés (Order.AssignedTo)
- Modifier OrderService pour appliquer ce filtre
- L'administrateur voit tout
- Ajouter les tests unitaires

## Vérification des stories "Terminé"

Après avoir implémenté les 5 stories "En cours", vérifie que les 9 stories marquées "Terminé" ont bien du code fonctionnel :
1. `dotnet build` compile sans erreur
2. `dotnet test` passe tous les tests
3. Les controllers correspondants existent et ont les bons endpoints
4. Les services implémentent la logique décrite dans les Gherkin

Si une story "Terminé" n'a pas de code réel → l'implémenter puis mettre à jour Xray.

## Conventions de code
- Nommage : PascalCase pour classes/méthodes, camelCase pour variables
- Chaque controller expose des endpoints RESTful sous `/api/[controller]`
- Les services sont injectés via DI (interfaces dans Application, implémentations dans Infrastructure)
- Les DTOs sont dans Application, jamais d'entité Domain dans les réponses API
- Les migrations EF Core sont dans Infrastructure/Data/Migrations
- Les tests suivent le pattern Arrange/Act/Assert avec Moq
- Authorization via attributs `[Authorize(Policy = "...")]` sur les controllers

## Git & Bitbucket
- Remote : origin → Bitbucket (softcom-aquaplan)
- Branche principale : main
- Convention de commit : `feat(AQ-XX): description`, `fix(AQ-XX): ...`, `test(AQ-XX): ...`
- Push après chaque Story complétée

## Agents disponibles
- `.claude/agents/xray-tester.md` — Gestion tests Xray Cloud (auth, import résultats, GraphQL)
- `.claude/agents/jira-updater.md` — Mise à jour stories Jira (transitions, commentaires)
- `.claude/agents/dev-senior.md` — Développeur senior .NET
- `.claude/agents/code-reviewer.md` — Code reviewer
- `.claude/agents/po.md` — Product Owner
- `.claude/agents/team-lead.md` — Team Lead

## Commence maintenant :
1. Lis CLAUDE.md et PLAN-DEVELOPPEMENT.md pour le contexte complet
2. Vérifie l'état avec `dotnet build AquaPlan.slnx` et `dotnet test AquaPlan.slnx`
3. Vérifie que .env contient JIRA_USER_EMAIL, JIRA_API_TOKEN, XRAY_CLIENT_ID, XRAY_CLIENT_SECRET
4. Lis l'agent xray-tester.md pour comprendre le workflow Xray
5. Commence par AQ-24 (Authentification OIDC) — lis d'abord le Gherkin
6. Pour chaque story : Gherkin → Code → Test → Build → Commit → Push → Xray → Jira
7. À la fin, fais un `dotnet build && dotnet test` global pour tout valider
8. Crée une Test Execution finale dans Xray avec tous les résultats v0.2
```
