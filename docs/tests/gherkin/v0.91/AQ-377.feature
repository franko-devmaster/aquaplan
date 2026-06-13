Feature: AQ-377 Transmission mandat - ping reseau a la demande

  Avant toute transmission d un mandat vers Limsophy, l UI effectue
  un ping GET /api/health. Si le ping echoue, un toast rouge s affiche
  sans mise en queue automatique (l utilisateur relance manuellement).

  Scenario: Transmission unitaire en ligne (happy path)
    Etant donne un Requerant clique sur "Transmettre" pour un mandat Completed
    Et un ping GET /api/health repond 200 en moins de 3s
    Quand l UI appelle POST /api/orders/{id}/transmit
    Alors la reponse est 200 OK (ou 202 selon implementation)
    Et le mandat passe en statut Transmitted
    Et un toast vert succes s affiche

  Scenario: Transmission offline - toast rouge sans queue
    Etant donne l utilisateur clique sur "Transmettre" en mode avion
    Quand le ping GET /api/health echoue (timeout 3s)
    Alors un toast rouge s affiche "Pas de reseau, reessayez"
    Et aucun appel POST /transmit n est emis
    Et aucune action n est mise en queue

  Scenario: Bulk transmit - ping prealable avant confirmation
    Etant donne un Admin clique sur "Tout transmettre" (bulk v0.9 AQ-345)
    Quand le ping prealable echoue
    Alors le dialog de confirmation ne s ouvre pas
    Et un toast unique "Pas de reseau, reessayez" s affiche
    Et aucun appel bulk-transmit n est emis
