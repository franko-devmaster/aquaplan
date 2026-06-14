@ui @regression
Feature: Page de connexion

  Verification du workflow de connexion via l'interface utilisateur.

  Scenario: Affichage de la page de connexion
    Given l'utilisateur est sur la page "/login"
    Then la page affiche "Connexion"

  Scenario: Connexion reussie avec un compte administrateur
    Given l'utilisateur est sur la page "/login"
    When l'utilisateur se connecte avec "admin@aquaplan.ch" et "Admin123!"
    Then l'utilisateur est redirige vers "/"
    And la page affiche "Tableau de bord"

  Scenario: Redirection vers login si non authentifie
    Given l'utilisateur est sur la page "/"
    Then l'utilisateur est redirige vers la page de connexion
