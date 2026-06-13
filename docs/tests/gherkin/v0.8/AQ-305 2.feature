Feature: AQ-305 Page /analysis-containers et menu Catalogue reorganise
  En tant qu'administrateur
  Je veux une page /analysis-containers et un ordre de menu coherent
  Afin de gerer visuellement le referentiel contenants

  Scenario: CRUD UI complet pour Admin
    Etant donne un Admin connecte sur /analysis-containers
    Quand il clique sur Ajouter un contenant, saisit les champs et valide
    Alors le nouveau contenant apparait dans la liste
    Et il peut l'editer via un dialog Modifier
    Et basculer IsActive via un toggle

  Scenario: Menu ordonne sur toutes les langues
    Etant donne la sidebar est affichee en fr, de ou en
    Quand j'ouvre la section Catalogue d'analyses
    Alors l'ordre des entrees est Programmes, Profils, Contenants
    Et les 3 libelles sont traduits dans les 3 langues

  Scenario: Non-Admin en lecture seule
    Etant donne un utilisateur non-Admin connecte
    Quand il accede a /analysis-containers
    Alors il voit la liste des contenants actifs
    Et les boutons Ajouter, Modifier et le toggle IsActive sont masques ou desactives
    Et une tentative d'appel API direct retourne 403 Forbidden
