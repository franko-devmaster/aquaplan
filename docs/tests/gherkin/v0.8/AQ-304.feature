Feature: AQ-304 API CRUD /api/containers
  En tant qu'administrateur
  Je veux creer, consulter, modifier et desactiver des contenants via API REST
  Afin de gerer le catalogue materiel sans acces base de donnees

  Scenario: CRUD complet avec role Admin
    Etant donne un utilisateur authentifie avec le role Admin
    Quand il envoie POST /api/containers avec un payload valide Code, Name, Material, VolumeMl, Color
    Alors l'API retourne 201 Created avec le ContainerDto cree
    Et GET /api/containers/{id} retourne ce contenant
    Et PUT /api/containers/{id} permet de modifier Name, Material, VolumeMl et Color
    Et PATCH /api/containers/{id}/toggle-status bascule IsActive

  Scenario: Code en doublon rejete
    Etant donne un contenant existant BACT-V250 dans le tenant courant
    Quand un Admin envoie POST /api/containers avec Code BACT-V250
    Alors l'API retourne 409 Conflict ou 400 Bad Request avec message explicite
    Et aucun contenant supplementaire n'est cree

  Scenario: Role non-Admin interdit en mutation
    Etant donne un utilisateur authentifie avec un role non-Admin Preleveur ou Viewer
    Quand il envoie POST, PUT ou PATCH sur /api/containers
    Alors l'API retourne 403 Forbidden
    Et GET /api/containers retourne 200 OK avec la liste
