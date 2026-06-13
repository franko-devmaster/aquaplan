Feature: AQ-309 Tuile dashboard renommee Mandats a finaliser
  En tant qu'utilisateur du dashboard
  Je veux que la tuile soit renommee Mandats a finaliser et agrege InProgress plus Completed
  Afin que le libelle reflete l'action attendue

  Scenario: Compteur agrege InProgress et Completed
    Etant donne une base avec 5 mandats InProgress et 3 mandats Completed
    Quand j'ouvre la page d'accueil
    Alors la tuile Mandats a finaliser affiche le compteur 8
    Et le libelle est Mandats a finaliser dans les 3 langues fr, de, en

  Scenario: Aucun mandat a finaliser
    Etant donne une base sans mandat InProgress ni Completed
    Quand j'ouvre la page d'accueil
    Alors la tuile affiche 0 sans erreur
    Et elle reste cliquable

  Scenario: Visibilite et portee par role et tenant
    Etant donne un utilisateur authentifie avec acces au dashboard
    Quand il consulte la tuile Mandats a finaliser
    Alors le compteur reflete uniquement les mandats de son tenant
    Et respecte les filtres de role Preleveur ne voit que ses mandats Admin voit tout
