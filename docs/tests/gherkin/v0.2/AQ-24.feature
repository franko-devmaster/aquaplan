Feature: Authentification via compte IdP

	Scenario: L'utilisateur accède à la page de connexion et est redirigé vers l'IdP
		Given L'utilisateur n'est pas authentifié
		When L'utilisateur accède à la page de connexion d'AquaPlan
		Then La page de connexion est affichée
			And Un bouton pour se connecter via l'IdP est visible

	Scenario: Après authentification réussie, l'utilisateur est redirigé vers AquaPlan avec ses droits
		Given L'utilisateur s'est authentifié auprès de l'IdP
			And Un compte utilisateur existe dans AquaPlan
		When L'utilisateur est redirigé vers AquaPlan
		Then L'utilisateur est authentifié
			And Les droits de l'utilisateur sont chargés
			And L'utilisateur est redirigé vers la page d'accueil

	Scenario: L'authentification échoue avec des identifiants invalides
		Given La page de connexion de l'IdP est affichée
		When L'utilisateur soumet des identifiants invalides
		Then Un message d'erreur est affiché
			And L'utilisateur n'est pas authentifié
