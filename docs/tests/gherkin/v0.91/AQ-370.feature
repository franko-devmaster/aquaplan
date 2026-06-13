Feature: AQ-370 Transition Assigned vers InProgress - ping reseau et pose du lock

  Au demarrage d une tournee par le preleveur, l application verifie
  la connexion puis transitionne Assigned -> InProgress et pose un
  verrou (IsLocked, LockedById, LockedAt) sur la tournee. Le snapshot
  offline est telecharge immediatement apres.

  Scenario: Preleveur demarre sa tournee en ligne avec pose du lock
    Etant donne une tournee T1 en statut Assigned assignee au preleveur U1
    Et un ping GET /api/health repond 200 en moins de 3s
    Quand U1 appelle PATCH /api/sampling-rounds/{T1}/start
    Alors la reponse est 200 OK
    Et le DTO retourne status=InProgress, isLocked=true, lockedById=U1, lockedAt != null
    Et un enregistrement audit "RoundStarted" est insere

  Scenario: Preleveur tente de demarrer une tournee non assignee (403)
    Etant donne une tournee T2 assignee au preleveur U2
    Quand le preleveur U1 appelle PATCH /api/sampling-rounds/{T2}/start
    Alors la reponse est 403 Forbidden
    Et la tournee T2 reste en statut Assigned en base
    Et aucun lock n est pose

  Scenario: Preleveur retente le demarrage alors que la tournee est deja InProgress
    Etant donne une tournee T1 deja en InProgress verrouillee par U1
    Quand U1 appelle a nouveau PATCH /api/sampling-rounds/{T1}/start
    Alors la reponse est 409 Conflict
    Et l etat du lock n est pas modifie
