Feature: AQ-308 Detail programme avec contenants uniques dedupliques
  En tant que preleveur ou responsable catalogue
  Je veux voir sur le detail d'un programme la liste unique des contenants requis
  Afin de preparer le materiel sans compter en double

  Scenario: Deduplication des contenants dans la reponse API
    Etant donne un programme contenant 3 profils P1 BACT-V250, P2 BACT-V250, P3 CHEM-PET500
    Quand on appelle GET /api/analysis-programs/{id}
    Alors le DTO retourne requiredContainers avec BACT-V250 et CHEM-PET500
    Et la page de detail affiche 2 contenants et non 3

  Scenario: Programme sans profils
    Etant donne un programme ne contenant aucun profil d'analyse
    Quand on consulte son detail
    Alors requiredContainers est un tableau vide
    Et la page affiche un message Aucun contenant requis

  Scenario: Acces lecture pour tous roles authentifies
    Etant donne un utilisateur authentifie avec un role quelconque Admin, Preleveur ou Viewer
    Quand il accede a /analysis-programs/{id}
    Alors il voit la liste dedupliquee des contenants
    Et la modification du programme reste soumise a Authorize Roles Admin
