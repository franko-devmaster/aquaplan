@smoke @regression
Feature: Smoke Tests - Verification de l'environnement

  Verification que l'environnement de test est fonctionnel
  avant toute campagne de test.

  Scenario: L'API backend est accessible
    Given l'API est accessible
    Then le endpoint "/api/health" retourne 200

  Scenario: Le frontend est accessible
    Given le frontend est accessible

  Scenario: L'authentification fonctionne
    Given l'API est accessible
    Then le login avec "admin@aquaplan.ch" et "Admin123!" retourne un token

  Scenario: L'API retourne des donnees avec authentification
    Given l'utilisateur est authentifie en tant que "administrateur"
    When une requete GET est envoyee a "/api/sampling-locations"
    Then le code de reponse est 200
    And la reponse contient au moins 1 elements
