# AquaPlan v0.3 — Prompt de développement

## Contexte

Tu es le développeur senior du projet AquaPlan, une application web de gestion des analyses de qualité d'eau pour le Canton de Fribourg. Le projet utilise .NET 10 / Angular 19 / PostgreSQL.

### État actuel (fin v0.2)

- **Backend** : ASP.NET Core API avec JWT + OIDC, 4 rôles (Requérant, Préleveur, Requérant-Préleveur, Administrator), isolation des données par distributeur, 59 tests unitaires passants
- **Frontend** : Angular 19 avec Material, pages admin (utilisateurs, rôles), mandats (liste, détail, création), lieux de prélèvement, SSO conditionnel, i18n (fr/en/de)
- **Infra** : Docker, Serilog, OpenTelemetry, EF Core + PostgreSQL, Bitbucket Pipelines
- **Branche** : `Main` sur Bitbucket

### Références

- **Code** : `CLAUDE.md` à la racine du projet (conventions, architecture, stack)
- **Plan** : `PLAN-DEVELOPPEMENT.md` (roadmap 4 releases)
- **Gherkin** : `docs/tests/gherkin/` (critères d'acceptation)
- **Confluence** : https://chfr.atlassian.net/wiki/spaces/CC/ (user stories par domaine)
- **Jira** : https://chfr.atlassian.net/jira/software/c/projects/AQ/summary

---

## Scope v0.3 — Release 2 : Mandats, planification et prélèvements

La v0.3 couvre les domaines Confluence 4, 6, 7 et 10 :

### Epic 2.1 — Création et gestion des mandats (domaine 4 : US-MAN)

7 stories couvrant le cycle de vie complet d'un mandat :

| Story | Description | Priorité |
|-------|-------------|----------|
| US-MAN-001 | Créer un mandat d'analyse (choix distributeur, LDP, analyses) | Haute |
| US-MAN-002 | Modifier un mandat (avant envoi) | Haute |
| US-MAN-003 | Valider et soumettre un mandat | Haute |
| US-MAN-004 | Annuler un mandat | Moyenne |
| US-MAN-005 | Consulter le détail d'un mandat | Haute |
| US-MAN-006 | Lister/filtrer les mandats (statut, distributeur, date) | Haute |
| US-MAN-007 | Générer le PDF d'un mandat (bon de prélèvement) | Basse |

**Travail nécessaire** :
- Backend : enrichir `OrderService` (workflow complet : Draft → Assigned → InProgress → SamplingCompleted → Validated → SentToLims → Completed/Cancelled), validation des transitions, filtrage avancé
- Backend : ajouter les entités `AnalysisCatalog`, `OrderAnalysis` (analyses demandées par mandat), `OrderSamplingLocation` (LDP par mandat)
- Frontend : formulaire de création multi-step (distributeur → LDP → analyses), page détail enrichie, filtres sur la liste
- PDF : génération du bon de prélèvement (QuestPDF ou similaire)

### Epic 2.2 — Plans de prélèvement & Tournées (domaines 6 + 10 : US-PLAN + US-TOUR)

5 stories :

| Story | Description | Priorité |
|-------|-------------|----------|
| US-PLAN-001 | Créer un plan de prélèvement (regrouper des LDP, fréquence) | Haute |
| US-PLAN-002 | Générer automatiquement les mandats depuis un plan | Haute |
| US-TOUR-001 | Organiser une tournée (regrouper des mandats par zone/date) | Moyenne |
| US-TOUR-002 | Optimiser l'ordre de passage (géo) | Basse |
| US-TOUR-003 | Affecter une tournée à un préleveur | Moyenne |

**Travail nécessaire** :
- Backend : entités `SamplingPlan`, `SamplingPlanLocation`, `SamplingTour`, `SamplingTourOrder`
- Backend : services de génération automatique de mandats depuis un plan
- Backend : optimisation géographique basique (tri par distance)
- Frontend : pages planification (calendrier/planning), affectation préleveur

### Epic 2.3 — Traitement des mandats / prélèvements (domaine 7 : US-PREV)

10 stories couvrant l'exécution terrain :

| Story | Description | Priorité |
|-------|-------------|----------|
| US-PREV-001 | Consulter la liste des mandats assignés (vue préleveur) | Haute |
| US-PREV-002 | Démarrer un prélèvement (arrivée sur site) | Haute |
| US-PREV-003 | Saisir les données de prélèvement (température, météo, GPS, notes) | Haute |
| US-PREV-004 | Prendre une photo du point de prélèvement | Basse |
| US-PREV-005 | Valider un prélèvement (signature électronique) | Moyenne |
| US-PREV-006 | Terminer un prélèvement (transition statut) | Haute |
| US-PREV-007 | Prélèvement non planifié (ad-hoc) | Moyenne |
| US-PREV-008 | Historique des prélèvements sur un LDP | Moyenne |
| US-PREV-009 | Exporter les données de prélèvement | Basse |
| US-PREV-010 | Pièces jointes sur un prélèvement | Basse |

**Travail nécessaire** :
- Backend : enrichir `Sampling` (photo, signature, pièces jointes via stockage fichiers), workflow de prélèvement
- Backend : service de stockage fichiers (Azure Blob / local pour dev)
- Frontend : formulaire de saisie terrain (optimisé mobile/tablette), géolocalisation
- Frontend : vue calendrier des mandats assignés

---

## Architecture cible v0.3

### Nouvelles entités Domain

```
AnalysisCatalog          (Id, Code, Name, Description, Category, IsActive)
OrderAnalysis             (Id, OrderId, AnalysisCatalogId, Notes)
OrderSamplingLocation     (Id, OrderId, SamplingLocationId, Sequence)
SamplingPlan              (Id, Name, Description, Frequency, TenantId, CreatedById)
SamplingPlanLocation      (Id, PlanId, SamplingLocationId, AnalysisCatalogId)
SamplingTour              (Id, Name, PlannedDate, PreleveurId, Status, TenantId)
SamplingTourOrder         (Id, TourId, OrderId, Sequence)
SamplingAttachment        (Id, SamplingId, FileName, ContentType, StoragePath, UploadedAt)
```

### Nouveaux endpoints API

```
POST   /api/orders/{id}/submit          → soumettre un mandat
POST   /api/orders/{id}/cancel          → annuler
POST   /api/orders/{id}/validate        → valider
GET    /api/orders?status=X&from=Y      → filtrage avancé

GET    /api/analysis-catalog            → catalogue d'analyses
POST   /api/analysis-catalog            → admin: créer une analyse

GET    /api/sampling-plans              → liste des plans
POST   /api/sampling-plans              → créer un plan
POST   /api/sampling-plans/{id}/generate → générer mandats

GET    /api/sampling-tours              → liste des tournées
POST   /api/sampling-tours              → créer une tournée
POST   /api/sampling-tours/{id}/assign  → affecter un préleveur

POST   /api/samplings                   → démarrer un prélèvement
PUT    /api/samplings/{id}              → saisir les données
POST   /api/samplings/{id}/validate     → valider un prélèvement
POST   /api/samplings/{id}/attachments  → upload pièce jointe
```

---

## Workflow de développement

Pour chaque story :
1. **Gherkin** — Écrire le fichier `.feature` dans `docs/tests/gherkin/v0.3/`
2. **Backend** — Entités, DTOs, Service (interface + implémentation), Controller
3. **Tests** — Tests unitaires (xUnit + FluentAssertions + Moq)
4. **Frontend** — Models, API service, datastore, page component
5. **Build** — `dotnet build` + `dotnet test` + `ng build`
6. **Commit** — `feat(AQ-XX): description courte`
7. **Push** — `git push origin Main`

### Ordre d'implémentation recommandé

```
Phase 1 : Catalogue d'analyses + entités enrichies
  → AnalysisCatalog, OrderAnalysis, OrderSamplingLocation
  → CRUD AnalysisCatalog (backend + frontend)
  → Enrichir le formulaire de création de mandat

Phase 2 : Workflow complet des mandats (US-MAN-001 à 006)
  → State machine OrderStatus (validations de transitions)
  → Endpoints submit/cancel/validate
  → Filtrage avancé (query params)
  → Frontend : formulaire multi-step, filtres, détail enrichi

Phase 3 : Plans de prélèvement (US-PLAN-001, 002)
  → Entités SamplingPlan + SamplingPlanLocation
  → Génération automatique de mandats
  → Frontend : page planification

Phase 4 : Tournées (US-TOUR-001, 002, 003)
  → Entités SamplingTour + SamplingTourOrder
  → Optimisation géographique basique
  → Frontend : page tournées, affectation

Phase 5 : Prélèvements terrain (US-PREV-001 à 006)
  → Enrichir Sampling (workflow, géoloc, météo)
  → Frontend : formulaire de saisie terrain

Phase 6 : Fonctionnalités secondaires (US-PREV-007 à 010, US-MAN-007)
  → Prélèvements non planifiés, historique, export, pièces jointes, PDF
```

---

## Conventions (rappel)

- Lire `CLAUDE.md` à la racine pour toutes les conventions de code
- **Tests obligatoires** pour tout code backend (controller + service)
- **Pas de tests Angular** (convention projet)
- **Sécurité** : `[Authorize]` sur tous les endpoints, isolation tenant, validation serveur
- **i18n** : mettre à jour fr.json, en.json, de.json pour chaque page Angular
- **Commits** : `feat(AQ-XX): description`, un commit par story ou groupe logique

---

## Données de test / Seeder

Le seeder existant (`RoleAndPermissionSeeder`) crée les rôles et un admin. Pour la v0.3, enrichir avec :
- 3-5 entrées dans `AnalysisCatalog` (analyses types : bactériologie, chimie, métaux)
- 2-3 `SamplingPlan` avec des LDP associés
- Quelques mandats en différents statuts pour tester le workflow

---

## Livrables attendus

1. Gherkin pour chaque story dans `docs/tests/gherkin/v0.3/`
2. Backend complet (entités, services, controllers, tests)
3. Frontend complet (pages, formulaires, navigation)
4. Tests passants (objectif : >100 tests unitaires)
5. Build clean (`dotnet build` 0 errors + `ng build` OK)
6. Commits poussés sur `Main`
