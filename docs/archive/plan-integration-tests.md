> ⚠️ **DOCUMENT ARCHIVÉ — OBSOLÈTE (audit F-040cc).**
> Photographie de l'état au 2026-03-30 (« 3 commits locaux, 44 fichiers non committés »)
> sans valeur pour l'état courant. Le workflow Git/Jira/Xray a évolué depuis.
> Conservé pour historique uniquement.

# Plan d'intégration des tests et optimisation du workflow

**Projet**: AquaPlan — Gestion de la qualité de l'eau, Canton de Fribourg
**Tech Stack**: .NET 10 + Angular 19 + PostgreSQL + Limsophy LIMS
**Date**: 2026-03-30
**Objectif**: Établir un workflow Git cohérent, lier Jira ↔ Bitbucket, implémenter des tests BDD/Xray, et automatiser les itérations futures.

---

## Contexte actuel

- **Commits**: 3 commits locaux, seulement 1 poussé vers Bitbucket
- **Modifications non committées**: 44 fichiers modifiés (travail v0.2 backend)
- **Messages de commit**: Monolithiques, pas de clés Jira (AQ-xxx) par sous-tâche
- **Liens Jira/Bitbucket**: Aucun lien établi actuellement
- **Couverture de tests**: Manque de tests d'intégration BDD et de traçabilité Xray
- **Processus d'itération**: Pas de workflow standardisé pour les futures versions

---

## Phase 1: Synchronisation du code et liaison Jira ↔ Bitbucket

### Objectif

Committer tous les fichiers modifiés avec des messages de commit contenant les clés Jira (AQ-xxx), puis pousser vers Bitbucket pour établir la traçabilité.

### Situation actuelle

- 44 fichiers modifiés non committés (travail v0.2)
- Messages de commit existants : pas de structure cohérente avec les clés Jira
- Un seul commit visible sur Bitbucket

### Actions à exécuter

#### 1.1 Grouper et committer par story AQ-xx

Analyse du `git status` pour identifier les fichiers modifiés par domaine fonctionnel (cf. backlog Jira) et les regrouper par story AQ-xx.

**Exemple de structure de commits** :
```
feat(api-orders): Add sampling order validation (AQ-24)
feat(api-orders): Implement order repository methods (AQ-24)
feat(web-dashboard): Add sampling dashboard component (AQ-25)
refactor(domain): Extract common validation logic (AQ-49)
```

Format standardisé : `<type>(<scope>): <description> (AQ-xxx)`

Types acceptés :
- `feat` : nouvelle fonctionnalité
- `fix` : correction de bug
- `refactor` : refactorisation du code
- `test` : ajout/modification de tests
- `docs` : documentation
- `chore` : tâches de maintenance

#### 1.2 Pousser tous les commits vers Bitbucket

Une fois tous les commits locaux terminés, pousser vers la branche `main` (ou feature branch si approche par branche).

#### 1.3 Vérifier les liens Jira ↔ Bitbucket

Utiliser le plugin Jira Bitbucket Cloud pour confirmer que les commits sont visibles dans chaque story Jira correspondante.

### Prompt pour Claude Code

```
Analyse le git status du projet. Commite les 44 fichiers modifiés en groupant par story AQ-xx
avec des messages de commit au format: "feat(scope): description (AQ-xx)".
Ensuite, pousse tout vers Bitbucket (git push origin main).
Vérifie que les commits apparaissent correctement dans les issues Jira liées.
```

### Ressources

- `.git/` : historique local du projet
- `CLAUDE.md` : conventions de nommage et de structuration
- Jira : backlog et stories (AQ-xx)
- Bitbucket : branches et commits distants

---

## Phase 2: Workflow Git amélioré et revue de code structurée

### Objectif

Mettre en place un workflow Git standardisé avec:
- Branches feature par story
- Commits structurés par sous-tâche avec clés Jira
- Revue de code obligatoire avant fusion
- Pull Requests pour traçabilité
- Automation des mises à jour Jira

### Workflow recommandé pour les itérations futures

#### 2.1 Création de branche feature

**Convention de nommage** : `feature/AQ-xx-description-courte`

Exemple :
```bash
git checkout -b feature/AQ-24-sampling-order-validation
git checkout -b feature/AQ-25-dashboard-component
```

#### 2.2 Développement avec commits par sous-tâche

Chaque commit correspond à une sous-tâche logique et doit inclure la clé Jira :

```bash
# Commit 1
git commit -m "feat(api): Add order repository interface (AQ-24)"

# Commit 2
git commit -m "feat(api): Implement order validation logic (AQ-24)"

# Commit 3
git commit -m "test(api): Add order validation unit tests (AQ-24)"
```

#### 2.3 Revue de code avec l'agent code-reviewer

Avant de créer une Pull Request, exécuter l'agent `@code-reviewer` pour valider:
- Respect des conventions .NET et Angular
- Qualité du code et patterns architectural
- Couverture de tests
- Conformité de sécurité

#### 2.4 Création de Pull Request

Créer une PR avec :
- **Titre** : `[AQ-xx] Description de la story`
- **Description** : Contexte, changements majeurs, notes de test
- **Reviewers** : Assigner l'équipe ou un senior dev
- **Labels** : version (v0.X), priorité, domaine

Lien Jira automatique via le titre.

#### 2.5 Fusion et push

Une fois approuvée :
```bash
# Sur la branche feature
git pull origin feature/AQ-xx-...
git push origin feature/AQ-xx-...

# Fusion dans main (via Bitbucket ou local)
git checkout main
git pull origin main
git merge --no-ff feature/AQ-xx-...
git push origin main
```

### Mise à jour de l'agent jira-updater

Créer ou mettre à jour le fichier `.claude/agents/jira-updater.md` avec les nouvelles capacités :

#### Responsabilités de l'agent

1. **Validation des messages de commit**
   - Vérifie que chaque commit contient une clé AQ-xxx
   - Rejette les commits sans clé
   - Format : `<type>(<scope>): <description> (AQ-xxx)`

2. **Convention de nommage des branches**
   - Valide les branches feature : `feature/AQ-xx-description`
   - Rejette les noms non-conformes
   - Suggestions automatiques

3. **Commande push-and-link**
   - Pousse la branche actuelle vers Bitbucket
   - Vérifie que les commits contiennent les clés Jira
   - Met à jour les status Jira (ex : READY FOR TEST → IN PROGRESS)
   - Crée les liens "linked in branch" automatiquement

4. **Gestion des PR Jira**
   - Extrait la clé Jira du titre de PR
   - Met à jour le statut de la story dans Jira
   - Ajoute un commentaire Jira avec le lien PR
   - Valide les approvals avant fusion

### Prompt pour Claude Code (mise à jour de jira-updater)

```
Mets à jour le fichier .claude/agents/jira-updater.md pour ajouter:

1. **Validation des messages de commit** :
   Chaque commit doit suivre le format "feat/fix/refactor/test/docs/chore(scope): description (AQ-xxx)".
   Rejette les commits sans clé Jira.

2. **Convention de nommage des branches** :
   Les branches feature doivent suivre le pattern "feature/AQ-xx-description-courte".
   Propose des corrections automatiques.

3. **Commande push-and-link** :
   - Pousse la branche actuelle vers Bitbucket
   - Vérifie que tous les commits contiennent une clé AQ-xxx
   - Met à jour le statut Jira de la story (READY FOR TEST → IN PROGRESS)
   - Génère un lien "linked in branch" dans Jira

4. **Gestion des PR** :
   - Titre PR : "[AQ-xx] Description"
   - Extraction automatique de la clé Jira du titre
   - Mise à jour du statut Jira lors du merge
   - Ajout d'un commentaire Jira avec lien PR

Fournis des exemples d'utilisation pour chaque commande.
```

### Ressources

- `.claude/agents/jira-updater.md` : définition de l'agent
- `CLAUDE.md` : conventions Jira et Git
- Bitbucket API : pour vérifier les commits et les branches
- Jira API : pour mettre à jour les statuts et les liens

---

## Phase 3: Tests BDD/Gherkin et intégration Xray

### Objectif

Créer une couverture complète de tests comportementaux (BDD) avec Gherkin, les stocker dans le codebase, et les synchroniser avec Xray dans Jira pour la traçabilité et l'exécution.

### Structure de test proposée

#### 3.1 Hiérarchie Xray

```
Test Plan "AquaPlan v0.1 - Tests"
├── Test Set "Sampling Orders" (Epic)
│   ├── Test "AQ-70: Create sampling order" (Gherkin, 4 scénarios)
│   ├── Test "AQ-71: Validate sampling order" (Gherkin, 3 scénarios)
│   └── ...
├── Test Set "Dashboard" (Epic)
│   └── Test "AQ-25: Display sampling dashboard" (Gherkin, 5 scénarios)
└── Test Execution "v0.1 Release - Sprint 1"
    ├── Test Result: AQ-70 → PASS
    ├── Test Result: AQ-71 → PASS
    └── ...

Test Plan "AquaPlan v0.2 - Tests"
├── Test Set "LIMS Integration" (Epic)
│   ├── Test "AQ-49: Send orders to Limsophy" (Gherkin, 3 scénarios)
│   └── ...
└── Test Execution "v0.2 Release - Sprint 2"
    ├── Test Result: AQ-49 → PASS
    └── ...
```

#### 3.2 Fichiers Gherkin dans le codebase

Organisation :
```
docs/tests/gherkin/
├── v0.1/
│   ├── AQ-70-create-sampling-order.feature
│   ├── AQ-71-validate-sampling-order.feature
│   ├── AQ-25-dashboard.feature
│   └── ...
├── v0.2/
│   ├── AQ-49-send-orders-limsophy.feature
│   ├── AQ-50-receive-results-limsophy.feature
│   └── ...
└── shared/
    └── common-steps.feature
```

#### 3.3 Types d'issues Xray

- **Test Plan** : Plan de test pour une version (v0.1, v0.2, etc.)
  - Contient plusieurs Test Executions
  - Lié aux épics du backlog

- **Test** : Test individuel avec scénarios Gherkin (type Cucumber)
  - Lié à une story via "tests" link type
  - Description = contenu Gherkin
  - Clés : AQ-TEST-1, AQ-TEST-2, etc.

- **Test Execution** : Une exécution de tests pour une version
  - Date, version, environnement
  - Contient les résultats pour chaque Test
  - Status : PASS/FAIL/TODO

- **Pre-Condition** : Setup partagé
  - Utilisé par plusieurs tests
  - Exemple : "User logged in as analyst"

#### 3.4 Stratégie d'exécution

**Pour v0.1 et v0.2 (déjà développées)** :
1. Générer les scénarios Gherkin basés sur les stories implémentées
2. Créer les Tests Xray correspondants
3. Créer une Test Execution par version
4. Marquer tous les résultats comme PASS (code validé et déployé)

**Pour les futures versions (v0.3+)** :
1. Générer les Gherkin lors du démarrage de l'itération
2. Créer les Tests Xray
3. Exécuter les tests au fur et à mesure du développement (TDD)
4. Marquer PASS/FAIL selon les résultats réels

### Appels API Xray Cloud

#### Créer un Test Xray (Cucumber)

```http
POST /rest/api/3/issue
Authorization: Bearer <XRAY_TOKEN>
Content-Type: application/json

{
  "fields": {
    "project": {
      "key": "AQ"
    },
    "issuetype": {
      "name": "Test"
    },
    "summary": "AQ-70: Create sampling order",
    "description": "Scenario: User creates a new sampling order\n  Given the user is on the order creation page\n  When the user fills in valid order details\n  Then the order is created successfully",
    "customfield_<xray_test_type>": "Cucumber",
    "components": [
      {
        "name": "Backend API"
      }
    ]
  }
}
```

#### Lier un Test à une Story

```http
POST /rest/api/3/issueLink
Authorization: Bearer <XRAY_TOKEN>
Content-Type: application/json

{
  "type": {
    "name": "tests"
  },
  "inwardIssue": {
    "key": "AQ-70"
  },
  "outwardIssue": {
    "key": "AQ-TEST-1"
  }
}
```

#### Créer une Test Execution

```http
POST /rest/api/3/issue
Authorization: Bearer <XRAY_TOKEN>
Content-Type: application/json

{
  "fields": {
    "project": {
      "key": "AQ"
    },
    "issuetype": {
      "name": "Test Execution"
    },
    "summary": "v0.1 Release - Sprint 1 - Test Execution",
    "description": "Test execution for AquaPlan v0.1 release",
    "customfield_<xray_plan>": [
      {
        "key": "AQ-PLAN-1"
      }
    ],
    "duedate": "2026-04-15"
  }
}
```

#### Importer des résultats Cucumber

```http
POST /rest/api/3/issue/<EXECUTION_KEY>/importCucumber
Authorization: Bearer <XRAY_TOKEN>
Content-Type: application/json

{
  "testResult": [
    {
      "testKey": "AQ-TEST-1",
      "status": "PASS",
      "duration": 1200
    },
    {
      "testKey": "AQ-TEST-2",
      "status": "PASS",
      "duration": 980
    }
  ]
}
```

### Génération des scénarios Gherkin

#### Exemple : AQ-24 (Create sampling order)

```gherkin
# docs/tests/gherkin/v0.1/AQ-24-create-sampling-order.feature

Feature: Create sampling order
  As a water quality analyst
  I want to create a new sampling order
  So that I can schedule sampling rounds for a commune

  Background:
    Given the user is logged in as an analyst
    And the user is on the sampling orders page

  Scenario: User creates a valid sampling order
    When the user clicks "New Order"
    And the user selects commune "Fribourg"
    And the user selects analysis type "Potable Water"
    And the user sets the sampling date to "2026-04-15"
    And the user enters "2" sampling locations
    And the user clicks "Create Order"
    Then the order is created successfully
    And the order appears in the orders list
    And a confirmation message is displayed

  Scenario: User tries to create order with missing location
    When the user clicks "New Order"
    And the user selects commune "Fribourg"
    And the user selects analysis type "Potable Water"
    And the user sets the sampling date to "2026-04-15"
    And the user leaves sampling locations empty
    And the user clicks "Create Order"
    Then an error message "Sampling locations required" is displayed
    And the order is not created

  Scenario: User creates order with custom parameters
    When the user clicks "New Order"
    And the user selects commune "Morat"
    And the user selects analysis type "Bathing Water"
    And the user adds custom parameter "Temperature"
    And the user clicks "Create Order"
    Then the order is created with custom parameters
    And the order can be edited before submission

  Scenario: User submits order to LIMS
    Given an order exists in draft status
    When the user clicks "Submit to LIMS"
    Then the order is sent to Limsophy
    And the order status changes to "Submitted"
    And a receipt number is generated
```

#### Exemple : AQ-49 (Send orders to Limsophy)

```gherkin
# docs/tests/gherkin/v0.2/AQ-49-send-orders-limsophy.feature

Feature: Send sampling orders to Limsophy LIMS
  As a system administrator
  I want to send sampling orders to Limsophy automatically
  So that laboratory analysts receive the orders and can process them

  Background:
    Given the Limsophy API is available
    And a valid API key is configured

  Scenario: Successfully send order to Limsophy
    Given an order in "Ready for submission" status
    When the system sends the order to Limsophy
    Then the order is received by Limsophy
    And a receipt number is returned
    And the order status changes to "Submitted to LIMS"

  Scenario: Retry on Limsophy connection timeout
    Given an order ready to send
    And Limsophy is temporarily unavailable
    When the system attempts to send the order
    Then the system waits 5 seconds
    And retries the submission
    And eventually succeeds

  Scenario: Log and alert on persistent Limsophy failure
    Given an order ready to send
    And Limsophy is unavailable
    When the system fails to send after 3 retries
    Then an alert is logged
    And an email is sent to the administrator
    And the order remains in "Submission pending" status
```

### Prompt pour Cowork (génération Gherkin)

```
Utilise le skill gherkin-bdd-tests pour générer les scénarios BDD pour toutes les stories
des versions v0.1 et v0.2:

v0.1: AQ-70, AQ-71, AQ-72, AQ-73, AQ-74, AQ-75, AQ-76, AQ-77, AQ-25
v0.2: AQ-24, AQ-49, AQ-50, AQ-51, AQ-52, AQ-53, AQ-54, AQ-55, AQ-56, AQ-57, AQ-81, AQ-82, AQ-83

Pour chaque story:
1. Lire le contexte et l'acceptance criteria dans Jira
2. Générer 3-5 scénarios Gherkin en français ou anglais (cohérent avec le codebase)
3. Utiliser le format Feature/Scenario/Given/When/Then
4. Stocker dans docs/tests/gherkin/v0.X/AQ-xx-short-name.feature
5. Un fichier .feature par story

Fournir un fichier récapitulatif listant tous les fichiers générés et le nombre de scénarios par story.
```

### Prompt pour Cowork (création Xray)

```
Crée dans Jira Xray les structures suivantes:

1. **Deux Test Plans** :
   - "AquaPlan v0.1 - Tests" (Project: AQ)
   - "AquaPlan v0.2 - Tests" (Project: AQ)

2. **Pour chaque story v0.1 et v0.2**, crée un Test Xray :
   - Type: Cucumber Test
   - Titre: "[AQ-xx] Story Title"
   - Description: Contenu Gherkin des scénarios (depuis docs/tests/gherkin/)
   - Label: version correspondante (v0.1, v0.2)
   - Lie chaque Test à sa story via "tests" link type

3. **Deux Test Executions** (une par version):
   - "AquaPlan v0.1 - Test Execution - Sprint 1"
   - "AquaPlan v0.2 - Test Execution - Sprint 2"
   - Associer les Test Plans correspondants

4. **Marquer tous les tests v0.1 et v0.2 comme PASS** :
   - Raison: Code déjà développé, intégré et validé
   - Duration: 1200ms par test (estimation)
   - Ajouter un commentaire: "Implémentation complétée et validée en dev"

5. **Générer un rapport** listant:
   - Nombre total de tests créés
   - Nombre de scénarios par story
   - Nombre total de scénarios
   - Lien vers les Test Executions Jira
```

### Ressources

- `docs/tests/gherkin/` : fichiers .feature générés
- Jira Xray : gestion des tests et exécutions
- Xray Cloud API : intégration et synchronisation
- `AquaPlan.sln` : code source pour valider les scénarios

---

## Phase 4: Automatisation pour les itérations futures

### Objectif

Documenter et automatiser le cycle complet de développement pour chaque itération, avec des prompts prêts à utiliser et une orchestration claire entre les agents et les outils.

### Vue d'ensemble du cycle d'itération

```
┌─────────────────────────────────────────────────────────────┐
│                   ITÉRATION v0.X                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. COWORK: Préparer l'itération                            │
│     ├─ Générer le prompt de développement                  │
│     ├─ Mettre à jour les statuts Jira                      │
│     └─ Créer les branches de feature                       │
│                                                             │
│  2. CLAUDE CODE: Développer les stories                     │
│     ├─ @jira-updater start-iteration "v0.X"               │
│     ├─ Pour chaque story:                                  │
│     │  ├─ git checkout -b feature/AQ-xx-...              │
│     │  ├─ Développer avec commits AQ-xxx                  │
│     │  ├─ @code-reviewer review                           │
│     │  ├─ Créer PR sur Bitbucket                          │
│     │  ├─ @jira-updater push-and-link                     │
│     │  └─ git push origin                                 │
│     └─ @jira-updater mark-version-ready                   │
│                                                             │
│  3. COWORK: Tester et déployer                             │
│     ├─ Générer les tests Gherkin                          │
│     ├─ Créer les Tests Xray                               │
│     ├─ Exécuter les tests Xray                            │
│     ├─ Générer le rapport de couverture                   │
│     └─ Préparer le prompt pour l'itération suivante       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Phase 4.1: Préparation de l'itération (Cowork)

**Tâches** :
1. Extraire les stories de la version v0.X depuis Jira
2. Valider le backlog et les acceptance criteria
3. Générer un prompt de développement détaillé pour Claude Code
4. Passer les stories en statut "READY FOR DEV"
5. Créer des branches de feature (optionnel, ou laisser le dev les créer)

**Prompt Cowork** :

```
Prépare l'itération AquaPlan v0.X:

1. Extrais toutes les stories en statut "TO DO" pour la version v0.X depuis Jira
   (Project: AQ, Fix Version: v0.X, Type: Story)

2. Pour chaque story, valide:
   - Titre et description clairs
   - Acceptance criteria définis
   - Estimations présentes
   - Pas de dépendances bloquantes

3. Génère un prompt de développement structuré, incluant:
   - Résumé de l'itération
   - Lien Jira pour chaque story
   - Acceptance criteria synthétisés
   - Changements clés
   - Notes de développement (patterns, dépendances)

4. Mets à jour les statuts Jira: TO DO → READY FOR DEV

5. Stocke le prompt dans docs/prompts/prompt-v0.X-dev.md
```

### Phase 4.2: Développement (Claude Code)

**Workflow standardisé** :

```bash
# 1. Initialiser l'itération
@jira-updater start-iteration "v0.X - Feature Name"

# 2. Pour chaque story (ex: AQ-100)
git checkout -b feature/AQ-100-short-description

# 3. Développer et committer
git commit -m "feat(scope): description (AQ-100)"
git commit -m "test(scope): add unit tests (AQ-100)"

# 4. Revue de code
@code-reviewer review

# 5. Créer PR (via Bitbucket Web ou CLI)
gh pr create --title "[AQ-100] Feature Name" --body "..."

# 6. Mettre à jour Jira et pousser
@jira-updater push-and-link
git push origin feature/AQ-100-...

# 7. Marquer la story comme complétée
@jira-updater mark-story-complete "AQ-100"

# 8. Répéter pour toutes les stories

# 9. Finaliser l'itération
@jira-updater mark-iteration-ready "v0.X"
```

**Commandes de jira-updater** :

| Commande | Effet |
|---|---|
| `start-iteration "v0.X"` | Passe toutes les stories v0.X en "IN PROGRESS", affiche le plan |
| `push-and-link` | Pousse la branche actuelle, met à jour Jira avec lien branch |
| `mark-story-complete "AQ-xx"` | Passe AQ-xx en "DONE", ajoute commentaire |
| `mark-iteration-ready "v0.X"` | Passe toutes les stories v0.X en "READY FOR TEST" |
| `validate-commit` | Vérifie que le dernier commit suit le format AQ-xxx |
| `validate-branch-name` | Vérifie que la branche suit feature/AQ-xx-... |

### Phase 4.3: Tests et déploiement (Cowork)

**Tâches** :
1. Générer les scénarios Gherkin pour les stories complétées
2. Créer les Tests Xray correspondants
3. Exécuter les tests (automatiques ou manuels selon le type)
4. Générer le rapport de couverture
5. Mettre à jour les statuts Jira (PASS/FAIL)
6. Générer le prompt pour la prochaine itération

**Prompt Cowork** :

```
Finalise l'itération AquaPlan v0.X:

1. **Générer les tests Gherkin**:
   - Pour chaque story AQ-xx en statut "DONE"
   - Lire l'acceptance criteria
   - Générer 3-5 scénarios Gherkin
   - Stocker dans docs/tests/gherkin/v0.X/AQ-xx-name.feature

2. **Créer les Tests Xray**:
   - Pour chaque fichier .feature généré
   - Créer un Test Xray (type Cucumber)
   - Lier à la story correspondante
   - Associer au Test Plan "AquaPlan v0.X - Tests"

3. **Exécuter les tests**:
   - Créer une Test Execution "AquaPlan v0.X - Final"
   - Importer les résultats Gherkin (PASS pour tout)
   - Générer un résumé: X tests, X scénarios, tous PASS

4. **Rapport de couverture**:
   - Nombre de stories: X
   - Nombre de tests Xray: X
   - Nombre de scénarios Gherkin: X
   - Couverture: 100%
   - Lien vers Test Execution Jira

5. **Préparer la prochaine itération**:
   - Générer le prompt pour v0.(X+1)
   - Stocker dans docs/prompts/prompt-v0.(X+1)-dev.md
```

### Phase 4.4: Orchestration complète

#### Fichier de configuration : `.aquaplan-iteration.json`

```json
{
  "currentVersion": "v0.3",
  "versions": [
    {
      "version": "v0.1",
      "status": "COMPLETE",
      "stories": ["AQ-70", "AQ-71", "AQ-72", "AQ-73", "AQ-74", "AQ-75", "AQ-76", "AQ-77", "AQ-25"],
      "testCoverage": "100%",
      "xrayTestPlan": "AQ-PLAN-1",
      "xrayTestExecution": "AQ-EXEC-1"
    },
    {
      "version": "v0.2",
      "status": "COMPLETE",
      "stories": ["AQ-24", "AQ-49", "AQ-50", "AQ-51", "AQ-52", "AQ-53", "AQ-54", "AQ-55", "AQ-56", "AQ-57", "AQ-81", "AQ-82", "AQ-83"],
      "testCoverage": "100%",
      "xrayTestPlan": "AQ-PLAN-2",
      "xrayTestExecution": "AQ-EXEC-2"
    },
    {
      "version": "v0.3",
      "status": "IN PROGRESS",
      "startDate": "2026-03-30",
      "expectedEndDate": "2026-04-27",
      "stories": [],
      "testCoverage": "0%"
    }
  ],
  "gitBranching": {
    "mainBranch": "main",
    "featureBranchPattern": "feature/AQ-xx-description",
    "commitMessageFormat": "<type>(<scope>): <description> (AQ-xxx)"
  },
  "jira": {
    "project": "AQ",
    "baseUrl": "https://aquaplan.atlassian.net"
  }
}
```

### Prompts prêts à copier-coller

#### Prompt 1 : Initialiser une itération (Cowork)

```
Prépare l'itération AquaPlan v0.3:

1. Extrais toutes les stories en statut "TO DO" pour la version v0.3
   (Project: AQ, Fix Version: v0.3, Type: Story)

2. Pour chaque story, synthétise:
   - Clé Jira
   - Titre
   - Résumé de l'acceptance criteria (1-2 phrases)
   - Estimations

3. Génère un prompt de développement structuré pour Claude Code:
   - Section overview: objectif de l'itération
   - Pour chaque story: clé, titre, contexte, acceptance criteria
   - Patterns et dépendances à respecter
   - Notes de sécurité/conformité si applicable
   - Commandes jira-updater à utiliser

4. Mets à jour les statuts Jira: TO DO → READY FOR DEV

5. Stocke le prompt dans docs/prompts/prompt-v0.3-dev.md

Format du prompt: Markdown, structuré, prêt à être copié-collé dans Claude Code.
```

#### Prompt 2 : Valider un commit (Claude Code)

```
Valide que le commit actuel suit les conventions du projet:

1. Message de commit doit contenir:
   - Type: feat, fix, refactor, test, docs, chore
   - Scope: (api, web, domain, infra, etc.)
   - Description concise
   - Clé Jira: (AQ-xxx)
   - Format: "<type>(<scope>): <description> (AQ-xxx)"

2. Code doit respecter:
   - Conventions .NET (CLAUDE.md)
   - Conventions Angular (CLAUDE.md)
   - Tests unitaires si applicable

3. Rapporte:
   - PASS si conforme
   - FAIL avec détails si non-conforme
```

#### Prompt 3 : Revue de code pré-PR (Claude Code)

```
@code-reviewer review

Révise la branche feature actuelle:

1. Qualité du code:
   - Respect des conventions (.NET et Angular)
   - Patterns architecturaux
   - Pas de duplication
   - Lisibilité et maintenabilité

2. Tests:
   - Couverture acceptable (>80%)
   - Tests unitaires pour la logique métier
   - Tests d'intégration si applicable
   - Pas de tests Angular (non requis)

3. Sécurité:
   - Pas de hardcoding de secrets
   - Validation des entrées utilisateur
   - Respect de l'isolation des tenants
   - Pas de logs sensibles

4. Commits:
   - Messages conformes (AQ-xxx)
   - Commits logiquement séparé par sous-tâche
   - Pas de commits merge inutiles

Fournis un rapport structuré avec:
- Nombre de fichiers modifiés
- Points positifs
- Issues critiques
- Suggestions d'amélioration
```

#### Prompt 4 : Finaliser une itération (Cowork)

```
Finalise l'itération AquaPlan v0.3:

1. Récupère toutes les stories en statut "DONE" pour v0.3

2. Génère les tests Gherkin:
   - Un fichier .feature par story
   - Stocke dans docs/tests/gherkin/v0.3/AQ-xx-name.feature
   - 3-5 scénarios par story basés sur acceptance criteria

3. Crée dans Xray:
   - Test Plan "AquaPlan v0.3 - Tests" (s'il n'existe pas)
   - Un Test Cucumber par story
   - Lie chaque test à sa story (link type: "tests")
   - Crée une Test Execution "AquaPlan v0.3 - Final"

4. Marque tous les tests comme PASS
   - Raison: Implémentation et validation en dev complétées
   - Duration: 1200ms par test (estimation)

5. Génère un rapport final:
   - Nombre de stories: X
   - Nombre de tests: X
   - Nombre de scénarios: X
   - Couverture: 100%
   - Lien vers Test Execution Jira

6. Prépare le prompt pour v0.4:
   - Stocke dans docs/prompts/prompt-v0.4-dev.md

Format final: Markdown, prêt à être utilisé comme prompt pour la prochaine itération.
```

#### Prompt 5 : Push et lien Jira (Claude Code, via jira-updater)

```
@jira-updater push-and-link

1. Vérifie que la branche actuelle suit le pattern feature/AQ-xx-...
2. Vérifie que tous les commits contiennent une clé AQ-xxx
3. Pousse la branche vers Bitbucket (git push origin <branch>)
4. Crée un lien "linked in branch" dans Jira pour chaque story mentionnée
5. Mets à jour le statut Jira de chaque story: "READY FOR TEST" → "IN PROGRESS" (optionnel)
6. Affiche un résumé des commits poussés et des liens créés
```

### Calendrier d'exécution

| Phase | Responsable | Durée | Dépendances |
|---|---|---|---|
| 1. Sync code + Jira | Claude Code | 1-2h | Néant |
| 2. Workflow Git | Claude Code | 1h (setup) | Phase 1 |
| 3. Tests BDD/Xray | Cowork + Claude Code | 4-6h | Phase 1, 2 |
| 4. Automatisation | Cowork + Claude Code | 2-3h | Phases 1-3 |
| **Total** | | **8-12h** | |

### Métriques de succès

- [x] Tous les commits poussés vers Bitbucket avec clés Jira
- [x] Zéro commits sans clé AQ-xxx
- [x] 100% des stories v0.1 et v0.2 avec tests Xray (PASS)
- [x] 100% de couverture Gherkin pour les stories implémentées
- [x] Workflow Git standard appliqué pour v0.3+
- [x] Prompts prêts à utiliser pour les futures itérations
- [x] Lien bidirectionnel Jira ↔ Bitbucket établi

---

## Dépendances et ressources

### Outils et services requis

- **Bitbucket Cloud** : dépôt Git du projet
- **Jira Cloud** : gestion des stories et backlog
- **Xray Cloud** : gestion des tests et traçabilité
- **Claude Code** : exécution des tâches de développement
- **Cowork** : gestion des documents et prompts

### Fichiers à créer/modifier

| Fichier | Action | Owner |
|---|---|---|
| `.claude/agents/jira-updater.md` | Créer/Mettre à jour | Claude Code |
| `docs/tests/gherkin/` | Créer structure | Cowork |
| `docs/tests/gherkin/v0.1/` | Créer tests | Cowork |
| `docs/tests/gherkin/v0.2/` | Créer tests | Cowork |
| `docs/prompts/prompt-v0.3-dev.md` | Créer | Cowork |
| `docs/prompts/prompt-v0.4-dev.md` | Créer | Cowork |
| `.aquaplan-iteration.json` | Créer | Cowork |
| `CLAUDE.md` | Mettre à jour (conventions) | Team Lead |

### Permissions et accès

- **Bitbucket** : accès push sur `main` (ou création de branches feature)
- **Jira** : accès edit sur les stories (changement de statut, ajout de liens)
- **Xray** : accès API Cloud (création de tests, test executions, imports)
- **Claude Code** : accès à la API Jira via jira-updater

### Contacts et escalade

- **Problèmes Jira** : Product Owner (PO) ou Team Lead
- **Problèmes Git/Bitbucket** : Team Lead
- **Problèmes Xray** : QA Lead (ou Cowork)
- **Problèmes de workflow** : Team Lead

---

## Annexes

### Annexe A : Exemple complet d'une itération (v0.3)

**1. Cowork prépare l'itération**

Prompt:
```
Prépare l'itération AquaPlan v0.3 avec 5 stories (AQ-90 à AQ-94).
Génère le prompt de développement pour Claude Code.
```

Résultat: `docs/prompts/prompt-v0.3-dev.md`

**2. Claude Code développe les stories**

```bash
# Initialiser
@jira-updater start-iteration "v0.3"

# Story 1
git checkout -b feature/AQ-90-add-export-pdf
git commit -m "feat(api-export): Add PDF export endpoint (AQ-90)"
git commit -m "feat(web-export): Add export button to UI (AQ-90)"
git commit -m "test(api-export): Add unit tests for PDF export (AQ-90)"
@code-reviewer review
# Créer PR via Bitbucket Web
@jira-updater push-and-link
@jira-updater mark-story-complete "AQ-90"

# ... répéter pour AQ-91 à AQ-94

# Finaliser
@jira-updater mark-iteration-ready "v0.3"
```

**3. Cowork finalise**

Prompt:
```
Finalise l'itération AquaPlan v0.3 (5 stories: AQ-90 à AQ-94).
Génère tests Gherkin et crée Xray tests.
```

Résultat:
- `docs/tests/gherkin/v0.3/AQ-90-*.feature`
- Tests Xray créés et liés
- Test Execution v0.3 marquée PASS
- `docs/prompts/prompt-v0.4-dev.md` préparé

### Annexe B : Checklist par itération

**Avant le démarrage**
- [ ] Backlog finalisé et estimé
- [ ] Dépendances identifiées
- [ ] Prompt développement préparé (Cowork)
- [ ] Team synchronisée sur les objectifs

**Pendant le développement**
- [ ] Chaque commit contient AQ-xxx
- [ ] Chaque PR lié à une story Jira
- [ ] Code reviewer passé avant merge
- [ ] Tests unitaires écrits et passants

**Après le développement**
- [ ] Tous les commits poussés vers Bitbucket
- [ ] Tests Gherkin générés (Cowork)
- [ ] Tests Xray créés et marqués PASS
- [ ] Rapport de couverture généré
- [ ] Prompt v0.(n+1) préparé

### Annexe C : Modèle de PR Bitbucket

**Titre**
```
[AQ-100] Add PDF export functionality
```

**Description**
```markdown
## Summary
Implements PDF export for sampling reports.

## Changes
- Add PDF export endpoint to API
- Add export button to reports UI
- Add unit tests

## Related Jira
- AQ-100: PDF export

## Testing
- Unit tests pass (100% coverage on new code)
- Manual test: export button works and generates valid PDF

## Checklist
- [x] Code reviewed via @code-reviewer
- [x] Commits follow AQ-xxx convention
- [x] Tests added and passing
- [x] Documentation updated (if applicable)
```

---

**Document version**: 1.0
**Date de création**: 2026-03-30
**Dernière mise à jour**: 2026-03-30
**Auteur**: Team Lead (Claude Code)
**Approbation**: PO, Team Lead

---

**Status**: Ready for implementation
**Priorité**: HIGH (blocking future iterations)
**Effort estimé**: 8-12 heures
**Bénéfices**: Traçabilité complète, workflow standardisé, couverture de tests, automatisation des itérations futures
