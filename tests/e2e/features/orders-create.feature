@api @regression
Feature: Création de mandat et ajout à une tournée
  En tant que mandataire
  Je veux créer un mandat et l'ajouter à une tournée
  Afin de planifier les prélèvements
  (Régression AQ — la validation [property:] des DTO records renvoyait 500 sur POST /api/orders)

  Scenario: Créer un mandat valide
    Given un utilisateur authentifié avec tenant_id valide
    When il crée un mandat pour un lieu et un programme valides
    Then le mandat créé porte un numéro

  Scenario: Ajouter un mandat à une tournée du même distributeur
    Given un utilisateur authentifié avec tenant_id valide
    When il crée un mandat pour la tournée existante
    And il ajoute ce mandat à la tournée
    Then la tournée contient le mandat

  Scenario: Créer un mandat sans distributeur est rejeté
    Given un utilisateur authentifié avec tenant_id valide
    When il crée un mandat sans distributeur
    Then il reçoit 400 Bad Request
