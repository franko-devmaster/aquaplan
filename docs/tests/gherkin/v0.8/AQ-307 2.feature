Feature: AQ-307 Order lie a AnalysisProgram et migration orphelins
  En tant que responsable catalogue
  Je veux qu'un mandat soit rattache a des programmes d'analyse avec pre-script orphelins
  Afin de travailler a la bonne granularite metier sans perte de donnees

  Scenario: Migration bascule de profils vers programmes
    Etant donne une base avec des mandats referencant des profils via order_analysis_profiles
    Et tous les profils sont deja rattaches a au moins un programme
    Quand la migration ReplaceOrderProfilesByPrograms est appliquee
    Alors la table order_analysis_programs existe
    Et elle contient une ligne DISTINCT par (OrderId, ProgramId) deduite via analysis_program_profiles
    Et la table order_analysis_profiles est supprimee
    Et aucun mandat ne perd de programme implicite

  Scenario: Profils orphelins automatiquement encapsules dans un programme
    Etant donne une base avec des profils non rattaches a aucun programme
    Quand la migration BackfillOrphanedProfilesAsPrograms s'execute
    Alors pour chaque profil orphelin de code XYZ un programme PROG-XYZ est cree
    Et une ligne analysis_program_profiles rattache le profil a ce programme
    Et un enregistrement est ajoute dans audit_log avec type OrphanProfileWrapped
    Et apres execution il n'y a plus aucun profil orphelin

  Scenario: Dialogs UI basculent sur programmes et rejettent profils
    Etant donne un utilisateur autorise a creer ou editer un mandat
    Quand il ouvre order-create-dialog, order-edit-dialog ou round-add-order-dialog
    Alors la picklist affiche des Programmes d'analyse et non plus des Profils
    Et le payload envoye au backend contient analysisProgramIds et non analysisProfileIds
    Et GET /api/orders/{id} retourne analysisPrograms[] avec les infos de chaque programme
    Et un appel forge avec analysisProfileIds retourne 400 Bad Request
