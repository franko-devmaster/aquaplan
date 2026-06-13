Feature: AQ-343 Refonte badges statuts et categories (pill-shape)

  Scenario: Happy path - style pill-shape applique
    Given un utilisateur visualise la liste des mandats /orders
    When il observe un badge de statut (ex. En cours)
    Then le badge a border-radius 4px
    And padding 2px 8px
    And font-size 11-12px et font-weight 500
    And background #F5F5F7 ou white selon contexte
    And border 1px solid rgba(0,0,0,0.08)
    And cette regle s'applique a tous les badges de l'app

  Scenario: Edge case - contextes colores preserves
    Given un statut critique (ex. Annule ou Erreur)
    When le badge est rendu
    Then la couleur semantique (rouge discret #C62828 ou orange #E65100) reste sur le texte
    And le fond reste clair (#FFEBEE ou pill neutre selon choix UX Lead)
    And la bordure subtile delimite le badge
    And le contraste AA WCAG est preserve

  Scenario: i18n et responsivite
    Given l'application en fr, de ou en
    When les badges sont rendus sur desktop et mobile
    Then leur taille reste constante (pas d'elargissement brutal sur texte long)
    And un long libelle est tronque avec ellipsis si necessaire
    And le composant app-badge est utilise partout
