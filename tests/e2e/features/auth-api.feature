@api @regression
Feature: Authentification API

  Verification des endpoints d'authentification et d'autorisation
  via l'API backend.

  Scenario: Login avec des identifiants valides
    Given l'API est accessible
    When l'utilisateur est authentifie avec "admin@aquaplan.ch" et "Admin123!"
    Then une requete GET est envoyee a "/api/auth/me"
    And le code de reponse est 200
    And la reponse contient le champ "email" avec la valeur "admin@aquaplan.ch"

  Scenario: Login avec des identifiants invalides
    Given l'API est accessible
    And l'utilisateur n'est pas authentifie
    When une requete POST est envoyee a "/api/auth/login" avec:
      """
      {"email": "inconnu@test.ch", "password": "mauvais"}
      """
    Then le code de reponse est 401

  Scenario: Acces sans token retourne 401
    Given l'utilisateur n'est pas authentifie
    When une requete GET est envoyee a "/api/sampling-locations"
    Then le code de reponse est 401

  Scenario: Le profil utilisateur contient les roles
    Given l'utilisateur est authentifie en tant que "administrateur"
    When une requete GET est envoyee a "/api/auth/me"
    Then le code de reponse est 200
    And la reponse contient le champ "roles"
