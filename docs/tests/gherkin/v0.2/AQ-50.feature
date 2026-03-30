Feature: Rôle Requérant (mandataire)

	Scenario: Le Requérant peut créer un mandat d'analyse
		Given L'utilisateur est authentifié avec le rôle Requérant
			And La page de création de mandat est affichée
		When L'utilisateur crée un nouveau mandat d'analyse
		Then Le mandat est créé avec succès
			And Le mandat est attribué au Requérant

	Scenario: Le Requérant peut suivre l'avancement des mandats
		Given L'utilisateur est authentifié avec le rôle Requérant
			And Au moins un mandat a été créé par cet utilisateur
		When L'utilisateur accède à la liste de ses mandats
		Then Tous les mandats créés par ce Requérant sont affichés
			And Le statut de chaque mandat est visible

	Scenario: Le Requérant ne peut PAS effectuer de prélèvements
		Given L'utilisateur est authentifié avec le rôle Requérant
		When L'utilisateur accède à l'interface AquaPlan
		Then La fonction de saisie des données de prélèvement n'est pas accessible
			And Le menu de prélèvement est masqué ou désactivé
