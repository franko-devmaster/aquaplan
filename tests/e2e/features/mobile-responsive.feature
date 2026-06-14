@mobile @ui @regression
Feature: Responsive mobile — exploration et détection de défauts de mise en page
  En tant qu'utilisateur sur smartphone
  Je veux que chaque écran s'affiche sans débordement
  Afin de pouvoir travailler sur le terrain

  Background:
    Given l'utilisateur est sur la page "/login"
    When l'utilisateur se connecte avec "admin@aquaplan.ch" et "Admin123!"
    Then l'utilisateur est redirige vers "/"

  Scenario: Tableau de bord
    Then je capture l'écran "mobile_dashboard"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Tournées
    Given l'utilisateur est sur la page "/sampling-rounds"
    Then je capture l'écran "mobile_tournees"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Plans de prélèvement
    Given l'utilisateur est sur la page "/sampling-plans"
    Then je capture l'écran "mobile_plans"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Résultats
    Given l'utilisateur est sur la page "/results"
    Then je capture l'écran "mobile_resultats"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Lieux de prélèvement
    Given l'utilisateur est sur la page "/sampling-locations"
    Then je capture l'écran "mobile_lieux"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Catalogue des profils d'analyse
    Given l'utilisateur est sur la page "/analysis-profiles"
    Then je capture l'écran "mobile_profils"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille

  Scenario: Gestion des utilisateurs
    Given l'utilisateur est sur la page "/admin/users"
    Then je capture l'écran "mobile_users"
    And la page ne déborde pas horizontalement
    And aucun contrôle ne déborde de sa taille
