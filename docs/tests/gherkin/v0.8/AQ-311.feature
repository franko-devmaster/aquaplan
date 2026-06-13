Feature: AQ-311 Vrai compteur LDP a valider pour Admin
  En tant qu'administrateur
  Je veux voir le nombre reel de LDP en attente de validation
  Afin de suivre ma file d'attente sans ouvrir la page dediee

  Scenario: Compteur reel affiche pour Admin
    Etant donne 4 LDP dans un etat UnValidated pour le tenant courant
    Et un utilisateur Admin connecte sur le dashboard
    Quand le dashboard est charge
    Alors un appel GET /api/sampling-locations/unvalidated est emis
    Et la tuile LDP a valider affiche 4
    Et le clic sur la tuile redirige vers /admin/validation-queue avec la liste des 4 LDP

  Scenario: Zero LDP a valider
    Etant donne aucun LDP non valide pour le tenant courant
    Quand l'Admin charge le dashboard
    Alors la tuile affiche 0 sans erreur
    Et reste cliquable

  Scenario: Endpoint unvalidated interdit pour non-Admin
    Etant donne un utilisateur non-Admin connecte
    Quand son dashboard s'affiche
    Alors la tuile LDP a valider n'emet pas d'appel GET /api/sampling-locations/unvalidated
    Et l'endpoint lui-meme retourne 403 Forbidden si requete directement
