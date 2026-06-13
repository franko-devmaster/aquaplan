Feature: AQ-372 Force-unlock Admin - transition InProgress vers Assigned

  Un Administrateur peut forcer le deverrouillage d une tournee
  bloquee (preleveur injoignable). La tournee repasse en Assigned,
  les colonnes de lock sont reinitialisees, un audit_log
  "RoundForceUnlocked" est insere. Seul Admin est autorise.

  Scenario: Admin force-unlock une tournee verrouillee (happy path)
    Etant donne une tournee T1 en InProgress verrouillee par U1 depuis 3h
    Et un Admin connecte
    Quand il appelle POST /api/sampling-rounds/{T1}/force-unlock
    Alors la reponse est 200 OK
    Et le DTO retourne status=Assigned, isLocked=false, lockedById=null, lockedAt=null
    Et un enregistrement audit_log "RoundForceUnlocked" est insere

  Scenario: Requerant ou Preleveur non autorise a force-unlock (403)
    Etant donne une tournee T1 verrouillee
    Quand un Requerant ou un Preleveur non proprietaire appelle POST /force-unlock
    Alors la reponse est 403 Forbidden
    Et un appel non authentifie retourne 401

  Scenario: Force-unlock sur tournee non verrouillee renvoie conflit
    Etant donne une tournee T3 en statut Assigned (non verrouillee)
    Quand un Admin appelle POST /api/sampling-rounds/{T3}/force-unlock
    Alors la reponse est 409 Conflict (ou 400) avec error="round.notLocked"
    Et aucun changement d etat
