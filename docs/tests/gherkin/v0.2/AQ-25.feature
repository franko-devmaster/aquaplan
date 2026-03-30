Feature: Single Sign-On (SSO) via EntraID

	Scenario: Un utilisateur déjà authentifié sur le réseau est automatiquement connecté
		Given L'utilisateur est authentifié sur le réseau via EntraID
			And L'utilisateur accède pour la première fois à AquaPlan
		When L'utilisateur accède à la page de connexion d'AquaPlan
		Then L'authentification EntraID est détectée automatiquement
			And L'utilisateur est authentifié sans intervention supplémentaire
			And La page d'accueil d'AquaPlan est affichée

	Scenario: Si le SSO échoue, l'utilisateur est redirigé vers la connexion classique
		Given L'utilisateur n'est pas authentifié sur le réseau
		When L'utilisateur accède à la page de connexion d'AquaPlan
		Then La page de connexion classique est affichée
			And L'utilisateur peut se connecter manuellement
