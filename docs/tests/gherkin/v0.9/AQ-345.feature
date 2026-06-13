Feature: AQ-345 Liste mandats - bulk Valider tout / Tout transmettre

  Scenario: Happy path - bulk validate
    Given 5 mandats InProgress et 3 mandats Completed pour le tenant courant
    And un Admin connecte sur /orders
    When il clique sur Valider tout
    Then une confirmation affiche "5 mandats passeront en statut Complete. Confirmer ?"
    When il confirme
    Then POST /api/orders/bulk-validate est appele
    And les 5 mandats InProgress passent en Completed
    And un toast "5 mandats valides" s'affiche
    And la liste est rafraichie

  Scenario: Edge case - aucun mandat eligible
    Given un tenant sans mandat InProgress
    When l'Admin ouvre /orders
    Then le bouton Valider tout est affiche mais desactive
    And son tooltip affiche Aucun mandat en cours a valider
    And aucun appel API n'est emis au clic
    And le bouton Tout transmettre est desactive si aucun mandat Completed

  Scenario: Permission - roles et LIMS
    Given un utilisateur Viewer (lecture seule)
    When il ouvre /orders
    Then les boutons Valider tout et Tout transmettre sont masques
    And un appel forge POST /api/orders/bulk-validate retourne 403 Forbidden
    And quand un Admin declenche Tout transmettre avec 3 mandats Completed, la transition Completed vers Transmitted invoque la logique Limsophy en mode idempotent
    And si Limsophy est indisponible, une reponse partielle est retournee
    And aucune modification de statut n'a lieu pour les echecs
