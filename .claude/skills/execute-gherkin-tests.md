# Skill: Execute Gherkin Tests (Exécution de tests Gherkin)

## Description
Exécute les scénarios Gherkin d'une Test Execution Xray directement dans l'application AquaPlan via le navigateur (Chrome MCP) et l'API, puis rapporte les résultats dans Xray.

## Prérequis

- Application AquaPlan démarrée : backend (port 5002) + frontend (port 4200)
- PostgreSQL démarré (docker compose up -d aquaplan-db)
- Données de seed chargées (scripts/seed_data.py)
- Credentials Xray/Jira dans `.env`
- Chrome MCP disponible pour les tests frontend

## Workflow

### 1. Récupérer les tests de l'exécution Xray

```bash
source .env
XRAY_TOKEN=$(curl -s -X POST "https://xray.cloud.getxray.app/api/v2/authenticate" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$XRAY_CLIENT_ID\",\"client_secret\":\"$XRAY_CLIENT_SECRET\"}" \
  | tr -d '"')
```

Récupérer les tests avec leur Gherkin :
```graphql
{
  getTestExecution(issueId: "<EXEC_INTERNAL_ID>") {
    issueId
    testRuns(limit: 100) {
      results {
        id
        status { name }
        test {
          issueId
          jira(fields: ["key", "summary"])
          testType { name }
          gherkin
        }
      }
    }
  }
}
```

### 2. Classifier chaque test

Analyser le Gherkin pour déterminer le type d'exécution :

| Pattern Gherkin | Type | Outil |
|---|---|---|
| `Given l'API ... est accessible` | Backend API | curl / HTTP calls |
| `Given l'utilisateur est sur la page` | Frontend UI | Chrome MCP |
| `Given la base de données contient` | Data setup | API calls |
| `When l'utilisateur clique` / `remplit` | Frontend UI | Chrome MCP |
| `When une requête POST/GET/PUT/DELETE` | Backend API | curl |

### 3. Exécuter les tests Backend (API)

Pour chaque test de type API :

1. **Given** : Préparer les données via API (login, création d'entités)
2. **When** : Exécuter la requête API décrite dans le Gherkin
3. **Then** : Vérifier la réponse (status code, body, headers)

```bash
# Exemple : login
TOKEN=$(curl -s -X POST http://localhost:5002/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@aquaplan.ch","password":"Admin123!"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

# Exemple : GET endpoint
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET http://localhost:5002/api/sampling-locations \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json")
HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | sed '$d')
```

### 4. Exécuter les tests Frontend (UI) via Chrome MCP

Pour chaque test de type UI :

1. **Naviguer** vers l'application : `mcp__Claude_in_Chrome__navigate` → `http://localhost:4200`
2. **Login** si nécessaire :
   - Naviguer vers `/login`
   - `mcp__Claude_in_Chrome__form_input` pour remplir email/password
   - `mcp__Claude_in_Chrome__computer` pour cliquer sur le bouton de connexion
3. **Exécuter les steps** :
   - `Given l'utilisateur est sur la page X` → `navigate` vers l'URL
   - `When l'utilisateur clique sur "Bouton"` → `computer` click ou `find` + click
   - `When l'utilisateur remplit le champ "Nom"` → `form_input`
   - `Then l'utilisateur voit "Texte"` → `get_page_text` ou `find` pour vérifier
   - `Then le tableau contient N lignes` → `get_page_text` + compter les lignes
   - `Then un message de succès s'affiche` → `find` le snackbar/toast

### 5. Mapping Gherkin → Actions Chrome MCP

| Step Gherkin (FR) | Action Chrome MCP |
|---|---|
| `l'utilisateur navigue vers "/url"` | `navigate` url=`http://localhost:4200/url` |
| `l'utilisateur est connecté en tant que "role"` | Login sequence (navigate /login, form_input, click) |
| `l'utilisateur clique sur "texte"` | `find` text → `computer` click coordinates |
| `l'utilisateur remplit "champ" avec "valeur"` | `form_input` selector + value |
| `l'utilisateur sélectionne "option" dans "select"` | `form_input` ou `computer` click |
| `la page affiche "texte"` | `get_page_text` → vérifier contenu |
| `le tableau contient N éléments` | `get_page_text` → compter lignes du tableau |
| `un message "texte" s'affiche` | `find` text dans la page |
| `l'utilisateur est redirigé vers "url"` | `read_page` → vérifier URL courante |
| `le bouton "texte" est désactivé` | `find` ou `javascript_tool` pour vérifier disabled |

### 6. Credentials de test

| Rôle | Email | Mot de passe |
|---|---|---|
| Administrateur SAAV | admin@aquaplan.ch | Admin123! |
| Mandataire SIE | m.dupont@sie-fribourg.ch | Test1234! |
| Mandataire Gruyère | a.martin@gruyere-energie.ch | Test1234! |
| Mandataire Glâne | p.favre@commune-romont.ch | Test1234! |
| Préleveur | j.schneider@labo-fribourg.ch | Test1234! |
| Lecteur | c.mueller@fr.ch | Test1234! |

### 7. Rapporter les résultats dans Xray

Pour chaque test exécuté, construire le résultat :

```bash
curl -s -X POST "https://xray.cloud.getxray.app/api/v2/import/execution" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $XRAY_TOKEN" \
  -d '{
    "testExecutionKey": "AQ-XXX",
    "tests": [
      {
        "testKey": "AQ-128",
        "status": "PASSED",
        "comment": "Tous les steps Gherkin validés avec succès.\n\nStep 1: Given ... ✅\nStep 2: When ... ✅\nStep 3: Then ... ✅"
      },
      {
        "testKey": "AQ-129",
        "status": "FAILED",
        "comment": "Échec au step 3.\n\nStep 1: Given ... ✅\nStep 2: When ... ✅\nStep 3: Then ... ❌ Attendu: 200, Reçu: 500\n\nErreur: Internal Server Error"
      }
    ]
  }'
```

### 8. Gestion des échecs

Pour chaque test FAILED :
1. Prendre une capture d'écran si test UI (`mcp__Claude_in_Chrome__computer` screenshot)
2. Documenter le step exact qui échoue dans le commentaire Xray
3. Créer un Bug dans Jira si le défaut est confirmé :
   ```bash
   curl -s -u "$JIRA_USER_EMAIL:$JIRA_API_TOKEN" \
     -X POST "https://chfr.atlassian.net/rest/api/3/issue" \
     -H "Content-Type: application/json" \
     -d '{
       "fields": {
         "project": {"key": "AQ"},
         "summary": "Bug: [description courte]",
         "issuetype": {"name": "Bug"},
         "description": {
           "type": "doc", "version": 1,
           "content": [{"type": "paragraph", "content": [{"type": "text", "text": "..."}]}]
         }
       }
     }'
   ```
4. Lier le bug au test run via `addDefectsToTestRun`

## Niveaux de test

### L1 — Smoke Test (pré-exécution)
Avant d'exécuter les tests Gherkin, vérifier :
- [ ] Backend répond : `curl -s http://localhost:5002/api/health` → 200
- [ ] Frontend accessible : `curl -s -o /dev/null -w "%{http_code}" http://localhost:4200` → 200
- [ ] Login fonctionne : POST `/api/auth/login` avec admin@aquaplan.ch → 200 + token
- [ ] API retourne des données : GET `/api/sampling-locations` avec token → 200

Si un smoke test échoue → STOP, ne pas exécuter les tests Gherkin. Signaler le problème.

### L2 — Tests API (Backend)
Exécuter les tests Gherkin de type API via curl/HTTP calls.

### L3 — Tests UI (Frontend)
Exécuter les tests Gherkin de type UI via Chrome MCP.

### L4 — Tests E2E (End-to-End)
Tests combinant API + UI dans un seul scénario (ex: créer via API, vérifier via UI).

## Rapport d'exécution

À la fin de l'exécution, produire un résumé :

```
=== Rapport d'exécution TE - AquaPlan vX.Y ===
Date : YYYY-MM-DD
Environnement : localhost (API:5002, UI:4200)

Smoke tests : ✅ 4/4 OK

Résultats :
  PASSED : X tests
  FAILED : Y tests
  TOTAL  : Z tests
  Taux   : X/Z (xx%)

Tests échoués :
  - AQ-xxx : [description] — Step N échoué : [détail]
  - AQ-yyy : [description] — Step N échoué : [détail]

Bugs créés :
  - AQ-xxx : [titre du bug]

Actions requises :
  - [ ] Corriger les bugs identifiés
  - [ ] Planifier un re-test
```
