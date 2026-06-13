Feature: AQ-336 Suppression entree sidebar /admin/validation-queue

  Scenario: Happy path - entree sidebar supprimee
    Given un Admin connecte
    When il ouvre la sidebar et deplie le groupe Administration
    Then l'entree File de validation LDP n'existe plus
    And les autres entrees Admin (Delegations, Tenants, etc.) sont inchangees

  Scenario: Edge case - acces direct URL
    Given un utilisateur tente d'acceder a /admin/validation-queue via URL directe
    When le router Angular resout la route
    Then une redirection 404 ou 302 vers /sampling-locations est effectuee
    And aucune erreur console n'apparait

  Scenario: Permission - liens entrants et i18n
    Given la tuile dashboard LDP a valider
    When un Admin clique dessus
    Then la navigation cible desormais /sampling-locations?validation=pending (filtre prefiltre)
    And plus /admin/validation-queue
    And toutes les cles i18n obsoletes (validationQueue.*) sont supprimees des 3 fichiers fr/de/en
