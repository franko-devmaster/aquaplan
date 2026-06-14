@mobile @ui @regression
Feature: Comportement des listes déroulantes sur mobile
  En tant qu'utilisateur sur smartphone
  Je veux qu'une liste déroulante se referme en tapant à côté
  Afin de ne pas avoir à recliquer dessus

  # AQ — sur mobile, le mat-select ne se referme pas au tap en dehors (OK sur desktop).
  Scenario: Une liste déroulante se referme en tapant en dehors
    Given l'utilisateur est sur la page "/login"
    When l'utilisateur se connecte avec "admin@aquaplan.ch" et "Admin123!"
    Then l'utilisateur est redirige vers "/"
    Given l'utilisateur est sur la page "/sampling-rounds"
    When j'ouvre la première liste déroulante
    And je tape en dehors de la liste
    Then la liste déroulante est fermée
