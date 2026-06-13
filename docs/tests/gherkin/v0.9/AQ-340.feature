Feature: AQ-340 Dialog prelevement - FormArray par contenant + unicite barcode

  Scenario: Happy path - saisie multi-contenants
    Given un mandat avec 3 contenants requis (Bacterio, Chimie, Pesticides)
    When un preleveur ouvre sampling-form-dialog
    Then 3 champs code-barres distincts sont presents, chacun prefixe du nom du contenant
    And chaque champ est un FormControl d'un FormArray cote Angular
    And la soumission avec les 3 codes saisis cree 3 sampling_containers

  Scenario: Edge case - champ vide autorise en brouillon
    Given un prelevement en statut InProgress
    When le preleveur soumet le dialog avec seulement 1 des 3 champs renseigne
    Then la soumission reussit (statut reste InProgress)
    And 1 seule ligne sampling_containers est creee
    And quand le preleveur tente la transition InProgress vers Completed, une erreur Codes-barres incomplets est affichee
    And la transition est bloquee tant que tous les contenants ne sont pas codes

  Scenario: Permission - unicite tenant
    Given un code-barres LAB-2026-001234 deja utilise sur un autre sampling du tenant T1
    When un preleveur T1 tente de saisir ce meme code sur un nouveau sampling
    Then l'API retourne 409 Conflict avec message Code-barres deja utilise
    And l'UI affiche l'erreur inline sur le champ concerne
    And le tenant T2 peut utiliser le meme code independamment
