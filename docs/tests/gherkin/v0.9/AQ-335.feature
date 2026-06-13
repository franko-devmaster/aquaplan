Feature: AQ-335 Dialog edition LDP - 4 boutons + canDelete API

  Scenario: Happy path - 4 boutons Admin sur LDP non valide non rattache
    Given un Admin ouvre le dialog d'edition d'un LDP non valide et non rattache
    When le dialog s'affiche
    Then 4 boutons sont visibles en bas Annuler, Supprimer, Valider, Enregistrer
    And le clic sur Valider appelle POST /api/sampling-locations/{id}/validate et ferme le dialog
    And le clic sur Supprimer appelle DELETE /api/sampling-locations/{id} apres confirmation

  Scenario: Edge case - LDP valide et rattache
    Given un LDP deja valide et rattache a un mandat
    When un Admin ouvre le dialog d'edition
    Then seuls Annuler et Enregistrer sont visibles
    And Valider est masque (IsValidated = true)
    And Supprimer est masque (canDelete = false)

  Scenario: Permission - non-Admin
    Given un utilisateur Requerant (non-Admin)
    When il ouvre le dialog d'edition d'un LDP
    Then Valider est toujours masque (action Admin-only)
    And Supprimer reste visible si canDelete = true
    And l'endpoint DELETE applique la politique d'autorisation backend
