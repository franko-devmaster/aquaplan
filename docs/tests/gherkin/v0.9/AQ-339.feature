Feature: AQ-339 Endpoint GET /api/orders/{id}/required-containers

  Scenario: Happy path - deduplication par ContainerId
    Given un mandat M1 avec 2 programmes
    And programme P1 contient profils Pr1 (BACT-V250) et Pr2 (BACT-V250)
    And programme P2 contient profil Pr3 (CHEM-PET500)
    When GET /api/orders/{M1.Id}/required-containers est appele
    Then l'API retourne 200 OK avec 2 objets RequiredContainerDto
    And la liste contient BACT-V250 et CHEM-PET500 une seule fois
    And la liste est triee par code croissant (stable)

  Scenario: Edge case - mandat avec sampling existant
    Given un mandat M1 avec un sampling contenant deja des sampling_containers
    When GET /api/orders/{M1.Id}/required-containers est appele
    Then pour chaque contenant requis, existingBarcode est pre-rempli avec le barcode actuel si present
    And existingBarcode reste null si le contenant n'a pas encore ete saisi

  Scenario: Permission - securite
    Given un mandat appartenant au tenant T1
    When un utilisateur du tenant T2 requete GET /api/orders/{M1.Id}/required-containers
    Then l'API retourne 404 Not Found
    And un utilisateur non authentifie recoit 401 Unauthorized
