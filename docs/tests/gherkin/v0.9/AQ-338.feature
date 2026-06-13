Feature: AQ-338 Entite SamplingContainer + migration + backfill legacy barcode

  Scenario: Happy path - migration et backfill
    Given une base AquaPlan v0.8 avec des samplings contenant sample_barcode renseigne
    When la migration AddSamplingContainers est appliquee
    Then la table sampling_containers existe avec colonnes Id, SamplingId, ContainerId, Barcode, BarcodeScannedAt, TenantId, CreatedAt
    And pour chaque sampling avec un sample_barcode non nul, une ligne sampling_containers est creee en best-effort
    And un index unique filtre (TenantId, Barcode) WHERE Barcode IS NOT NULL est cree
    And la colonne samplings.sample_barcode est conservee (legacy, read-only UI)

  Scenario: Edge case - mandat sans programme ni profil
    Given un sampling dont le mandat n'a aucun programme rattache
    When la migration de backfill s'execute
    Then aucune ligne sampling_containers n'est creee pour ce sampling
    And un enregistrement audit_log de type SamplingContainerBackfillSkipped est insere avec la cause
    And la migration continue sans echouer

  Scenario: Permission - suppression sampling cascade
    Given un sampling avec 3 sampling_containers associes
    When le sampling est supprime (DELETE /api/samplings/{id})
    Then les 3 sampling_containers sont supprimes en cascade (FK ON DELETE CASCADE)
    And la suppression d'un Container reference par un sampling_container est bloquee (FK ON DELETE RESTRICT)
    And l'isolation tenant est respectee
