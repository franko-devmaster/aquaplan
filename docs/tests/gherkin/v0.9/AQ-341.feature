Feature: AQ-341 Resume tournee - Materiel a preparer

  Scenario: Happy path - agregation multi-mandats
    Given une tournee T1 avec 4 mandats actifs
    And chaque mandat M1 a M4 requiert les contenants BACT-V250 et CHEM-PET500
    When un preleveur ouvre /sampling-rounds/{T1.Id}
    Then une section Materiel a preparer affiche une bullet-list
    And la liste contient 4 x Bouteille PET chimie
    And la liste contient 4 x Bouteille verre sterile microbiologie
    And l'ordre est trie par code contenant croissant (stable)

  Scenario: Edge case - mandat annule exclu
    Given une tournee T1 avec 4 mandats dont 1 en statut Cancelled
    When le resume est calcule
    Then le mandat Cancelled est exclu de l'agregation
    And la bullet-list affiche 3 x contenants (et non 4)
    And un mandat Draft, InProgress, Completed ou Transmitted est inclus

  Scenario: Permission - tenant
    Given une tournee appartenant au tenant T1
    When un utilisateur du tenant T2 tente GET /api/sampling-rounds/{T1.Id}
    Then l'API retourne 404 Not Found
    And un preleveur T1 voit uniquement sa tournee avec son resume materiel
