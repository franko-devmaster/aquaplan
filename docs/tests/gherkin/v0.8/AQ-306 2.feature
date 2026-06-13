Feature: AQ-306 AnalysisProfile.ContainerId obligatoire avec backfill
  En tant que responsable catalogue
  Je veux que chaque profil soit rattache a un contenant avec backfill par Category
  Afin que le preleveur voie le flacon a preparer

  Scenario: Backfill par categorie applique correctement
    Etant donne une base avec profils aux categories Bacteriology, Chemistry, Physical, Other
    Quand la migration AddContainerIdToAnalysisProfile est appliquee
    Alors chaque profil Bacteriology a ContainerId BACT-V250
    Et chaque profil Chemistry a ContainerId CHEM-PET500
    Et chaque profil Physical a ContainerId PHY-V100
    Et chaque profil Other a ContainerId CHEM-PET500
    Et la colonne container_id est NOT NULL apres backfill
    Et la contrainte FK est OnDelete Restrict

  Scenario: Creation profil sans contenant rejetee
    Etant donne un Admin sur le formulaire de creation d'un profil
    Quand il soumet sans selectionner de contenant
    Alors la validation frontend bloque l'envoi avec un message Contenant requis
    Et un appel API POST /api/analysis-profiles sans ContainerId retourne 400 Bad Request

  Scenario: Suppression contenant utilise bloquee
    Etant donne un contenant BACT-V250 utilise par au moins un profil
    Quand un Admin tente de supprimer physiquement ce contenant
    Alors la base rejette la suppression et l'API retourne 409 Conflict
    Et l'Admin peut desactiver le contenant via toggle-status
    Et un contenant inactif n'apparait plus dans la picklist de creation de profil
