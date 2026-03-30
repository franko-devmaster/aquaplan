Feature: Rôle Préleveur

	Scenario: Le Préleveur peut consulter les mandats attribués
		Given L'utilisateur est authentifié avec le rôle Préleveur
			And Des mandats lui sont attribués
		When L'utilisateur accède à la liste des mandats
		Then Les mandats attribués au Préleveur sont affichés
			And Les mandats d'autres utilisateurs ne sont pas visibles

	Scenario: Le Préleveur peut saisir les données de prélèvement
		Given L'utilisateur est authentifié avec le rôle Préleveur
			And Un mandat est attribué au Préleveur
			And La page de saisie de prélèvement est affichée
		When L'utilisateur saisit les données de prélèvement
		Then Les données sont sauvegardées avec succès
			And Le statut du mandat est mis à jour

	Scenario: Le Préleveur ne peut PAS créer de mandats planifiés
		Given L'utilisateur est authentifié avec le rôle Préleveur
		When L'utilisateur accède à l'interface AquaPlan
		Then La fonction de création de mandat n'est pas accessible
			And Le menu de création de mandat est masqué ou désactivé
