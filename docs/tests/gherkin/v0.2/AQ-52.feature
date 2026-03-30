Feature: Rôle Requérant-Préleveur

	Scenario: Le Requérant-Préleveur cumule les permissions des deux rôles
		Given L'utilisateur est authentifié avec le rôle Requérant-Préleveur
		When L'utilisateur accède à l'interface AquaPlan
		Then L'utilisateur dispose des permissions du rôle Requérant
			And L'utilisateur dispose des permissions du rôle Préleveur

	Scenario: L'interface affiche les fonctionnalités des deux rôles
		Given L'utilisateur est authentifié avec le rôle Requérant-Préleveur
		When L'utilisateur accède au menu principal
		Then La fonction de création de mandat est accessible
			And La fonction de saisie de prélèvement est accessible
			And Les deux fonctionnalités sont visibles et activées
