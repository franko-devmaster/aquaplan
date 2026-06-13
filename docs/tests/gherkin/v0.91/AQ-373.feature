Feature: AQ-373 Endpoint offline-snapshot complet

  En une seule requete, le backend retourne le DTO agrege contenant
  la tournee, ses mandats, les LDP actifs et valides du distributeur,
  les programmes/profils/contenants references et les samplings
  existants. Endpoint en lecture seule. Autorise pour le preleveur
  assigne ou Admin.

  Scenario: Preleveur assigne telecharge le snapshot complet
    Etant donne une tournee T1 InProgress assignee au preleveur U1
    Et T1 contient 10 mandats avec plusieurs programmes et contenants
    Et le distributeur de T1 compte des LDP actifs et valides
    Quand U1 appelle GET /api/sampling-rounds/{T1}/offline-snapshot
    Alors la reponse est 200 OK
    Et le DTO contient round, orders[], samplingLocations[], analysisPrograms[], analysisProfiles[], containers[], samplings[]
    Et samplingLocations contient tous les LDP actifs+valides du distributeur (pas uniquement ceux des mandats)

  Scenario: Preleveur non assigne recoit 403
    Etant donne une tournee T1 assignee au preleveur U1
    Quand le preleveur U2 appelle GET /api/sampling-rounds/{T1}/offline-snapshot
    Alors la reponse est 403 Forbidden
    Et un appel non authentifie retourne 401

  Scenario: Snapshot sur tournee inexistante renvoie 404
    Etant donne un identifiant de tournee aleatoire et inexistant
    Quand un user authentifie appelle GET /api/sampling-rounds/{id}/offline-snapshot
    Alors la reponse est 404 Not Found
    Et aucune donnee n est exposee
