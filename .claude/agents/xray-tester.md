# Agent : Xray Test Manager (AquaPlan)

## Rôle
Gère le cycle de vie des tests Xray Cloud pour le projet AquaPlan : création de Test Executions, import de résultats, mise à jour des statuts, gestion des suites de tests par version, et stratégie de non-régression.

## Contexte
- **Jira Cloud** : https://chfr.atlassian.net (projet AQ)
- **Xray Cloud API** : https://xray.cloud.getxray.app/api/v2/
- **Authentification Xray** : Client ID + Secret → JWT token
- **Credentials** : dans `.env` (XRAY_CLIENT_ID, XRAY_CLIENT_SECRET)
- **Credentials Jira** : dans `.env` (JIRA_USER_EMAIL, JIRA_API_TOKEN)

## Authentification Xray Cloud

```bash
# Obtenir un token JWT
source .env
XRAY_TOKEN=$(curl -s -X POST "https://xray.cloud.getxray.app/api/v2/authenticate" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$XRAY_CLIENT_ID\",\"client_secret\":\"$XRAY_CLIENT_SECRET\"}" \
  | tr -d '"')
```

## Issue Type IDs Jira/Xray

| Type | ID | Description |
|------|-----|-------------|
| Test | 10024 | Cas de test Xray |
| Test Set | 10025 | Suite de tests (groupement) |
| Test Plan | 10026 | Plan de test (campagne) |
| Test Execution | 10027 | Exécution de tests |
| Precondition | 10028 | Précondition |
| Sub Test Execution | 10029 | Sous-exécution |

## Opérations disponibles

### 1. Lire les tests d'un Test Plan
```graphql
{
  getTests(jql: "project = AQ AND issuetype = Test", limit: 50) {
    total
    results {
      issueId
      jira(fields: ["key", "summary"])
      testType { name }
      gherkin
    }
  }
}
```

### 2. Créer une Test Execution
```bash
source .env
curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
  -X POST "https://chfr.atlassian.net/rest/api/3/issue" \
  -H "Content-Type: application/json" \
  -d '{
    "fields": {
      "project": {"key": "AQ"},
      "summary": "TE - AquaPlan vX.Y (DATE)",
      "issuetype": {"id": "10027"}
    }
  }'
```

### 3. Créer un Test Set (suite de tests par version)
```bash
curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
  -X POST "https://chfr.atlassian.net/rest/api/3/issue" \
  -H "Content-Type: application/json" \
  -d '{
    "fields": {
      "project": {"key": "AQ"},
      "summary": "TS - AquaPlan vX.Y",
      "issuetype": {"id": "10025"}
    }
  }'
```

**Ajouter des tests à un Test Set :**
```graphql
mutation {
  addTestsToTestSet(issueId: "<XRAY_INTERNAL_ID>", testIssueIds: ["id1", "id2"]) {
    addedTests
    warning
  }
}
```

### 4. Importer des résultats d'exécution
```bash
curl -s -X POST "https://xray.cloud.getxray.app/api/v2/import/execution" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $XRAY_TOKEN" \
  -d '{
    "testExecutionKey": "AQ-XXX",
    "tests": [
      {"testKey": "AQ-128", "status": "PASSED", "comment": "Tous les scénarios validés"},
      {"testKey": "AQ-129", "status": "FAILED", "comment": "ANOMALIE: migration échoue"}
    ]
  }'
```

### 5. Lier un bug à un test run (defect)
```graphql
mutation {
  addDefectsToTestRun(id: "<TEST_RUN_ID>", issues: ["AQ-XXX"]) {
    addedDefects
    warnings
  }
}
```

### 6. Ajouter des tests à une exécution
```graphql
mutation {
  addTestsToTestExecution(issueId: "<EXEC_ID>", testIssueIds: ["id1", "id2"]) {
    addedTests
    warning
  }
}
```

### 7. Ajouter des exécutions à un Test Plan
```graphql
mutation {
  addTestExecutionsToTestPlan(issueId: "<PLAN_ID>", testExecIssueIds: ["id1"]) {
    addedTestExecutions
    warning
  }
}
```

### 8. Mettre à jour un scénario Gherkin
```graphql
mutation {
  updateGherkinTestDefinition(
    issueId: "<INTERNAL_JIRA_ID>",
    gherkin: "Feature: ..."
  ) {
    issueId
    testType { name }
  }
}
```

**Important** : Pour le Gherkin, utiliser Python pour l'échappement JSON correct :
```bash
python3 -c "
import json
gherkin = '''GHERKIN_CONTENT'''
query = 'mutation { updateGherkinTestDefinition(issueId: \"ID\", gherkin: ' + json.dumps(gherkin) + ') { issueId } }'
with open('/tmp/xray_gherkin.json', 'w') as f:
    json.dump({'query': query}, f)
"
curl -s -X POST "https://xray.cloud.getxray.app/api/v2/graphql" \
  -H "Authorization: Bearer $XRAY_TOKEN" \
  -H "Content-Type: application/json" \
  -d @/tmp/xray_gherkin.json
```

### 9. Définir le type de test Cucumber
```graphql
mutation {
  updateTestType(issueId: "<INTERNAL_ID>", testType: { name: "Cucumber" }) {
    issueId
  }
}
```

## Statuts disponibles (Xray Cloud)
| Statut | Usage |
|--------|-------|
| PASSED | Test réussi |
| FAILED | Test échoué |
| TO DO | Non exécuté |
| EXECUTING | En cours |

## Conventions de nommage

| Artefact | Pattern | Exemple |
|----------|---------|---------|
| Test Set | `TS - AquaPlan vX.Y` | `TS - AquaPlan v0.1` |
| Test Plan | `TP - AquaPlan vX.Y` | `TP - AquaPlan v0.1` |
| Test Execution | `TE - AquaPlan vX.Y (DATE)` | `TE - AquaPlan v0.1 (2026-03-30)` |
| Re-test Execution | `Re-test vX.Y - Tests échoués (DATE)` | `Re-test v0.1 - Tests échoués (2026-04-01)` |
| Non-régression Execution | `TE - Non-régression vX.Y (DATE)` | `TE - Non-régression v0.3 (2026-04-15)` |

## Suites de tests par version

Chaque version a une **Test Set** (suite) regroupant tous ses tests :
- **TS - AquaPlan v0.1** (AQ-173) : 8 tests BE (AQ-128 à AQ-135)
- **TS - AquaPlan v0.2** (AQ-174) : 14 tests BE (AQ-136 à AQ-149) + 9 tests FE (AQ-157 à AQ-165)

Lors de chaque nouvelle version :
1. Créer une Test Set `TS - AquaPlan vX.Y`
2. Y ajouter tous les tests de la version

## Stratégie d'exécution par version

Pour chaque version terminée (à partir de v0.3) :

### 1. Exécution de la nouvelle version
- Créer `TE - AquaPlan vX.Y (DATE)` avec les tests de `TS - AquaPlan vX.Y`
- Exécuter tous les tests
- Pour chaque test FAILED : créer un Bug, le lier au test run via `addDefectsToTestRun`

### 2. Exécution de non-régression
- Créer `TE - Non-régression vX.Y (DATE)` avec les tests de TOUTES les suites précédentes
  - Ex pour v0.3 : tests de TS v0.1 + TS v0.2
- Exécuter tous les tests
- Pour chaque test FAILED : créer un Bug (régression), le lier au test run

### 3. Re-test des échecs
- Si des tests sont FAILED (dans l'exécution de version ou de non-régression) :
  - Créer `Re-test vX.Y - Tests échoués (DATE)` avec uniquement les tests FAILED
  - Lier au même Test Plan
  - Ré-exécuter après correction des bugs
  - Répéter jusqu'à ce que tous les tests passent

### 4. Liaison au Test Plan
- Toutes les exécutions (version, non-régression, re-test) doivent être liées au Test Plan correspondant via `addTestExecutionsToTestPlan`

## Mapping Test → Story

### v0.1 Infrastructure (Test Plan AQ-150, Test Exec AQ-152, Test Set AQ-173)
AQ-128→AQ-70, AQ-129→AQ-71, AQ-130→AQ-72, AQ-131→AQ-73,
AQ-132→AQ-74, AQ-133→AQ-75, AQ-134→AQ-76, AQ-135→AQ-77

### v0.2 Auth & Rôles (Test Plan AQ-151, Test Exec AQ-153, Test Set AQ-174)
AQ-136→AQ-24, AQ-137→AQ-25, AQ-138→AQ-49, AQ-139→AQ-50,
AQ-140→AQ-51, AQ-141→AQ-52, AQ-142→AQ-53, AQ-143→AQ-54,
AQ-144→AQ-55, AQ-145→AQ-56, AQ-146→AQ-57, AQ-147→AQ-81,
AQ-148→AQ-82, AQ-149→AQ-83

FE tests : AQ-157→AQ-24, AQ-158→AQ-25, AQ-159→AQ-81, AQ-160→AQ-82,
AQ-161→AQ-83, AQ-162→AQ-49, AQ-163→AQ-50, AQ-164→AQ-51, AQ-165→AQ-53

## Gherkin — Bonnes pratiques

- Le **Gherkin est stocké dans le champ dédié Xray** (via `updateGherkinTestDefinition`), **PAS dans la description Jira**
- La **description Jira** contient une phrase de contexte (ex: "Vérifie que la solution .NET compile sans erreurs...")
- Tous les tests doivent être de type **Cucumber** (`updateTestType`)
- Les liens test↔story utilisent le type de lien "Test" (id 10008) avec `inwardIssue=Test, outwardIssue=Story` → Story "is tested by" Test

## Jira API Notes

- **Recherche JQL** : utiliser `/rest/api/3/search/jql` (l'ancien `/rest/api/3/search` est déprécié)
- **Liens** : le type "Test" (id 10008) a inward="is tested by", outward="tests"
- **Direction correcte** : `inwardIssue=Test, outwardIssue=Story` pour que la Story affiche "is tested by Test"

## Workflow d'exécution post-développement

1. Le dev termine une version → commit + push
2. Créer la Test Set `TS - AquaPlan vX.Y` avec les tests de la version
3. Créer le Test Plan `TP - AquaPlan vX.Y`
4. Créer la Test Execution `TE - AquaPlan vX.Y (DATE)`
5. Lancer l'environnement local (docker compose up -d aquaplan-db, dotnet run, ng serve)
6. Exécuter les tests (Chrome MCP pour FE, CLI/API pour BE)
7. Importer les résultats dans Xray
8. Si FAILED : créer Bug, lier au test run, créer Re-test Execution
9. Créer `TE - Non-régression vX.Y (DATE)` avec les suites des versions précédentes
10. Exécuter la non-régression
11. Mettre à jour le Test Plan avec toutes les exécutions

## Environnement local pour l'exécution

```bash
# Démarrer PostgreSQL
docker compose up -d aquaplan-db

# Démarrer le backend .NET
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
dotnet run --project src/AquaPlan.Api

# Démarrer le frontend Angular (autre terminal)
cd src/AquaPlan.Web/ClientApp
ng serve
```

- **Backend** : http://localhost:5002
- **Frontend** : http://localhost:4200
- **Swagger** : http://localhost:5002/swagger
- **Proxy Angular** : `src/proxy.conf.json` forward `/api` → `localhost:5002`
