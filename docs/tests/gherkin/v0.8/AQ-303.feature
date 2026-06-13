Feature: AQ-303 Entite Container et seed de 6 contenants
  En tant que preleveur et administrateur
  Je veux disposer d'un referentiel de contenants modelise dans le domaine AquaPlan
  Afin de decrire le materiel physique pour chaque analyse

  Scenario: Migration et seed - 6 contenants crees
    Etant donne une base AquaPlan a la version v0.7.1 sans table containers
    Quand la migration AddContainers est appliquee et le seeder AnalysisCatalogSeeder execute
    Alors la table containers existe avec les colonnes Id, Code, Name, Material, VolumeMl, Color, IsActive, TenantId
    Et elle contient exactement 6 lignes avec les codes BACT-V250, CHEM-PET500, CHEM-PEHD250, PHY-V100, PEST-V1000, ISOT-V60
    Et chaque contenant a un unique volume
    Et l'index unique sur TenantId et Code est cree

  Scenario: Unicite par tenant - contrainte de cle
    Etant donne un tenant T1 avec le contenant BACT-V250 deja seede
    Quand on tente d'inserer un second contenant avec le code BACT-V250 pour le tenant T1
    Alors l'insertion echoue avec une violation de contrainte d'unicite
    Et le meme code BACT-V250 peut coexister pour un tenant T2 different

  Scenario: Isolation tenant - filtrage global
    Etant donne deux tenants T1 et T2 avec chacun leurs 6 contenants seedes
    Quand un utilisateur authentifie du tenant T1 requete le DbSet Containers
    Alors seuls les 6 contenants du tenant T1 sont retournes
    Et aucun contenant du tenant T2 n'est visible
