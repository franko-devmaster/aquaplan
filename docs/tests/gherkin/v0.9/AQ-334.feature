Feature: AQ-334 Colonne Actions LDP - menu 3 points verticaux

  Scenario: Happy path - menu kebab avec 3 actions
    Given un Admin sur /sampling-locations section Reseau
    And un LDP non valide, non rattache a un mandat
    When il clique sur le menu 3 points verticaux de la ligne
    Then un mat-menu s'ouvre avec 3 items visibles Editer, Valider, Supprimer
    And la colonne Actions est positionnee a droite de la colonne Statut
    And le bouton corbeille d'avant n'existe plus

  Scenario: Edge case - LDP valide et rattache
    Given un LDP deja valide (IsValidated=true) et rattache a au moins un mandat
    When un Admin ouvre le menu kebab de cette ligne
    Then seul l'item Editer est present
    And Valider est masque (LDP deja valide)
    And Supprimer est masque (LDP rattache a un mandat)

  Scenario: Permission - roles non-Admin
    Given un utilisateur Requerant (non-Admin)
    When il ouvre le menu kebab d'un LDP non valide
    Then il voit Editer et Supprimer (si non rattache) mais pas Valider
    And un appel direct POST /api/sampling-locations/{id}/validate retourne 403 Forbidden
