Feature: AQ-344 Dashboard - filtrage tournees Brouillon + Assignee

  Scenario: Happy path - Admin voit Brouillon + Assignee
    Given 5 tournees en base 2 Draft, 1 Assigned, 1 InProgress, 1 Completed
    And un utilisateur Admin connecte
    When il ouvre la page d'accueil
    Then la section Tournees a venir affiche 3 tournees (2 Draft + 1 Assigned)
    And les tournees InProgress et Completed ne sont pas affichees
    And le tri par date de tournee croissante est respecte

  Scenario: Edge case - Preleveur voit Assignee seule
    Given un Preleveur connecte
    And 3 tournees lui sont assignees 1 Draft, 1 Assigned, 1 InProgress
    When il ouvre la page d'accueil
    Then seule la tournee Assignee est affichee
    And la Draft n'apparait pas
    And l'InProgress n'apparait pas non plus

  Scenario: Permission - isolation tenant
    Given 2 tenants T1 et T2 avec des tournees Assigned chacun
    When un utilisateur T1 ouvre le dashboard
    Then seules les tournees T1 sont affichees
    And aucune tournee T2 ne fuite
