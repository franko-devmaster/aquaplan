Feature: AQ-333 Bugfix ecran delegations + traduction round-add-order

  Scenario: Happy path - ecran delegations operationnel
    Given un tenant avec au moins 2 delegations en base
    And un utilisateur Admin connecte
    When il navigue vers /admin/delegations
    Then la liste des 2 delegations s'affiche sans erreur console
    And chaque ligne presente utilisateur delegue, role, periode, statut
    And l'utilisateur peut creer, editer et supprimer une delegation

  Scenario: Edge case - tenant sans delegation
    Given un tenant sans aucune delegation
    When l'Admin ouvre /admin/delegations
    Then un empty-state s'affiche (Aucune delegation configuree)
    And aucune erreur n'apparait en console ni dans les logs backend
    And le bouton Creer une delegation reste fonctionnel

  Scenario: i18n - cle analysisPrograms traduite
    Given un utilisateur autorise ouvre le dialog round-add-order-dialog
    When il consulte le champ de selection des programmes d'analyse
    Then le libelle affiche est "Programmes d'analyse" en fr, "Analyseprogramme" en de, "Analysis programs" en en
    And aucune chaine brute "orders.analysisPrograms" n'apparait dans l'UI
    And un utilisateur non autorise voit toujours un 403 sur /admin/delegations
