# Agent: Jira Status Updater

## Rôle

Cet agent automatise la mise à jour des statuts des tickets Jira pendant les itérations de développement. Il synchronise les statuts entre les subtâches, stories et épics en respectant la hiérarchie Jira et les règles de propagation de statut.

**Instance Jira** : `chfr.atlassian.net`
**Clé du projet** : `AQ`

## Pré-requis

### Configuration des variables d'environnement

Avant d'utiliser cet agent, configurez les variables d'environnement suivantes :

```bash
export JIRA_USER_EMAIL="your-email@example.com"
export JIRA_API_TOKEN="your_jira_api_token"
```

**Où obtenir le token API Jira** :
1. Accédez à https://id.atlassian.com/manage-profile/security/api-tokens
2. Créez un nouveau token API
3. Copiez le token et stockez-le de manière sécurisée

### Vérification de la configuration

```bash
# Tester la connexion
curl -s -u "${JIRA_USER_EMAIL}:${JIRA_API_TOKEN}" \
  -X GET https://chfr.atlassian.net/rest/api/3/myself | jq .
```

## Transitions de statut Jira

| Statut | ID Transition | Description |
|--------|---------------|-------------|
| Backlog | 11 | Ticket dans le backlog |
| Selected for Development | 21 | Ticket sélectionné pour le développement |
| En cours | 31 | Ticket en cours de développement |
| Terminé | 41 | Ticket complété |

## Commandes disponibles

### 1. `start-story <key>`

Démarre une story et son épic parent.

**Comportement** :
- Change la story au statut "En cours" (transition 31)
- Change l'épic parent au statut "En cours" (transition 31)

**Exemple** :
```bash
jira-updater start-story AQ-123
```

### 2. `complete-subtask <key>`

Marque une subtâche comme terminée et propage le statut vers la story parent.

**Comportement** :
- Change la subtâche au statut "Terminé" (transition 41)
- Vérifie si toutes les subtâches sœurs sont terminées
- Si oui : change la story parent au statut "Terminé" (transition 41)

**Exemple** :
```bash
jira-updater complete-subtask AQ-123
```

### 3. `complete-story <key>`

Marque une story comme terminée et propage le statut vers l'épic parent.

**Comportement** :
- Change la story au statut "Terminé" (transition 41)
- Vérifie si toutes les stories de l'épic parent sont terminées
- Si oui : change l'épic au statut "Terminé" (transition 41)

**Exemple** :
```bash
jira-updater complete-story AQ-123
```

### 4. `start-iteration <fixVersion>`

Démarre une itération en changeant le statut de toutes les stories.

**Comportement** :
- Récupère toutes les stories de la fixVersion spécifiée
- Change chaque story au statut "Selected for Development" (transition 21)
- Identifie les épics parents uniques
- Change chaque épic au statut "En cours" (transition 31)

**Exemple** :
```bash
jira-updater start-iteration "Sprint 2026-04"
```

### 5. `sync-status`

Synchronise les statuts de tous les tickets en cours.

**Comportement** :
- Récupère tous les tickets avec le statut "En cours"
- Pour chaque story : vérifie si toutes ses subtâches sont terminées
  - Si oui : change la story à "Terminé"
  - Ensuite : vérifie si toutes les stories de l'épic sont terminées
    - Si oui : change l'épic à "Terminé"
- Affiche un rapport de synchronisation

**Exemple** :
```bash
jira-updater sync-status
```

## Logique de propagation des statuts

### Règles de propagation

1. **Subtâche → Story** :
   - Quand une subtâche passe à "Terminé"
   - Si toutes les subtâches de la story sont "Terminé"
   - Alors la story passe à "Terminé"

2. **Story → Épic** :
   - Quand une story passe à "Terminé"
   - Si toutes les stories de l'épic sont "Terminé"
   - Alors l'épic passe à "Terminé"

3. **Démarrage d'itération** :
   - Toutes les stories de la fixVersion passent à "Selected for Development"
   - Les épics parents passent à "En cours"

### Cas spéciaux

- **Statuts non-transactionnels** : Si une transition n'est pas possible depuis le statut courant, l'agent rapporte une erreur mais continue le traitement des autres tickets
- **Épics sans stories** : Un épic ne peut passer à "Terminé" que si toutes ses stories sont terminées
- **Stories sans subtâches** : Une story peut être marquée directement comme terminée

## Implémentation

### Configuration de base pour les appels API

Tous les appels API utilisent l'authentification Basic avec le format `email:token` en base64.

**Fonction helper pour l'authentification** :

```bash
function jira_auth_header() {
  local auth_string="${JIRA_USER_EMAIL}:${JIRA_API_TOKEN}"
  local encoded=$(echo -n "$auth_string" | base64)
  echo "Authorization: Basic $encoded"
}

JIRA_INSTANCE="https://chfr.atlassian.net"
JIRA_API="/rest/api/3"
```

### Modèle pour obtenir un ticket

```bash
function get_issue() {
  local key=$1
  curl -s -X GET \
    -H "$(jira_auth_header)" \
    -H "Content-Type: application/json" \
    "${JIRA_INSTANCE}${JIRA_API}/issues/${key}" | jq .
}
```

### Modèle pour effectuer une transition

```bash
function transition_issue() {
  local key=$1
  local transition_id=$2

  curl -s -X POST \
    -H "$(jira_auth_header)" \
    -H "Content-Type: application/json" \
    -d "{\"transition\": {\"id\": \"${transition_id}\"}}" \
    "${JIRA_INSTANCE}${JIRA_API}/issues/${key}/transitions"
}
```

### Modèle pour requête JQL

```bash
function jql_search() {
  local jql=$1
  curl -s -X GET \
    -H "$(jira_auth_header)" \
    -H "Content-Type: application/json" \
    "${JIRA_INSTANCE}${JIRA_API}/search?jql=$(urlencode "$jql")&maxResults=100" | jq .
}
```

### Exemple : start-story

```bash
#!/bin/bash
# start-story AQ-123

KEY="AQ-123"

# Obtenir la story
STORY=$(get_issue "$KEY")
STORY_KEY=$(echo "$STORY" | jq -r '.key')

# Obtenir l'épic parent
EPIC_LINK=$(echo "$STORY" | jq -r '.fields.customfield_10008')

# Transition de la story à "En cours" (31)
echo "Démarrage de la story $STORY_KEY..."
transition_issue "$STORY_KEY" "31"

# Transition de l'épic à "En cours" (31)
if [ ! -z "$EPIC_LINK" ] && [ "$EPIC_LINK" != "null" ]; then
  echo "Démarrage de l'épic $EPIC_LINK..."
  transition_issue "$EPIC_LINK" "31"
fi
```

### Exemple : complete-subtask

```bash
#!/bin/bash
# complete-subtask AQ-123

KEY="AQ-123"

# Obtenir la subtâche
SUBTASK=$(get_issue "$KEY")
SUBTASK_KEY=$(echo "$SUBTASK" | jq -r '.key')

# Obtenir la story parent
PARENT_KEY=$(echo "$SUBTASK" | jq -r '.fields.parent.key')

# Transition de la subtâche à "Terminé" (41)
echo "Marquage de la subtâche $SUBTASK_KEY comme terminée..."
transition_issue "$SUBTASK_KEY" "41"

# Vérifier si toutes les subtâches de la story sont terminées
SUBTASKS=$(get_issue "$PARENT_KEY" | jq -r '.fields.subtasks[] | select(.fields.status.id != 10006)')

if [ -z "$SUBTASKS" ]; then
  echo "Toutes les subtâches de $PARENT_KEY sont terminées. Marquage de la story comme terminée..."
  transition_issue "$PARENT_KEY" "41"
fi
```

### Exemple : sync-status

```bash
#!/bin/bash
# sync-status

# Récupérer tous les tickets "En cours" du projet AQ
JQL='project = AQ AND status = "En cours"'
ISSUES=$(jql_search "$JQL" | jq -r '.issues[] | .key')

UPDATED_COUNT=0

for KEY in $ISSUES; do
  ISSUE=$(get_issue "$KEY")
  ISSUE_TYPE=$(echo "$ISSUE" | jq -r '.fields.issuetype.name')

  # Si c'est une story
  if [ "$ISSUE_TYPE" = "Story" ]; then
    # Vérifier si toutes les subtâches sont terminées
    INCOMPLETE_SUBTASKS=$(echo "$ISSUE" | jq -r '.fields.subtasks[] | select(.fields.status.id != 10006)')

    if [ -z "$INCOMPLETE_SUBTASKS" ]; then
      echo "Marquage de la story $KEY comme terminée..."
      transition_issue "$KEY" "41"
      UPDATED_COUNT=$((UPDATED_COUNT + 1))
    fi
  fi
done

echo "Synchronisation terminée. $UPDATED_COUNT ticket(s) mis à jour."
```

## Gestion des erreurs

Tous les appels API doivent gérer les erreurs de manière robuste :

```bash
function safe_transition() {
  local key=$1
  local transition_id=$2

  local response=$(curl -s -w "\n%{http_code}" -X POST \
    -H "$(jira_auth_header)" \
    -H "Content-Type: application/json" \
    -d "{\"transition\": {\"id\": \"${transition_id}\"}}" \
    "${JIRA_INSTANCE}${JIRA_API}/issues/${key}/transitions")

  local body=$(echo "$response" | head -n -1)
  local http_code=$(echo "$response" | tail -n 1)

  if [ "$http_code" -ne 204 ]; then
    echo "Erreur lors de la transition de $key: HTTP $http_code"
    echo "$body" | jq .
    return 1
  fi

  echo "Transition de $key vers transition $transition_id réussie"
  return 0
}
```

## Notes d'implémentation

- **Silent mode** : Tous les `curl` utilisent le flag `-s` pour mode silencieux
- **Encodage UTF-8** : Assurez-vous que le shell supporte UTF-8 pour les caractères accentués (français)
- **Validations** : Vérifiez toujours que les variables d'environnement sont définies avant de les utiliser
- **Idempotence** : Les transitions doivent être idempotentes - relancer une commande ne devrait pas causer d'erreur
- **Logging** : Chaque action doit afficher un message clair indiquant son statut
