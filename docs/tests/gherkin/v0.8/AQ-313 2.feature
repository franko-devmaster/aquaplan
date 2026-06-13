Feature: AQ-313 Dialog round-add-order toujours en creation
  En tant que preleveur ou chef d'equipe
  Je veux que le dialog Ajouter un mandat ouvre directement le formulaire de creation
  Afin de simplifier le workflow terrain

  Scenario: Ouverture dialog sans radio mode
    Etant donne une tournee affichee en edition
    Quand l'utilisateur clique Ajouter un mandat
    Alors le dialog s'ouvre directement sur le formulaire de creation d'un nouveau mandat
    Et aucun radio button Creer nouveau ou Lier existant n'est present
    Et la soumission cree un mandat et le rattache a la tournee

  Scenario: Tentative de re-rattachement via API refusee
    Etant donne un mandat M1 deja rattache a la tournee T1
    Quand un appel forge tente de rattacher M1 a la tournee T2
    Alors l'API retourne 409 Conflict ou 400 Bad Request avec message explicite
    Et M1 reste rattache a T1 uniquement

  Scenario: Role non autorise bloque
    Etant donne un utilisateur sans le role Preleveur ni Admin
    Quand il tente d'ouvrir le dialog d'ajout de mandat a une tournee
    Alors le bouton Ajouter un mandat n'est pas affiche
    Et l'appel API de creation de mandat rattache retourne 403 Forbidden
