Feature: Rôle Administrateur

	Scenario: L'Administrateur a accès à toutes les fonctionnalités
		Given L'utilisateur est authentifié avec le rôle Administrateur
		When L'utilisateur accède à l'interface AquaPlan
		Then Tous les menus et fonctionnalités sont accessibles
			And Les options d'administration sont visibles

	Scenario: L'Administrateur peut gérer les comptes et les rôles
		Given L'utilisateur est authentifié avec le rôle Administrateur
		When L'utilisateur accède à la page de gestion des utilisateurs
		Then L'Administrateur peut créer de nouveaux utilisateurs
			And L'Administrateur peut modifier les rôles des utilisateurs
			And L'Administrateur peut désactiver des comptes
