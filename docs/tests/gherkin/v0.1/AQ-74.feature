Feature: Configurer l'authentification

	Scenario: L'endpoint login retourne un JWT valide
		Given L'API AquaPlan est en cours d'exécution
			And Un utilisateur avec des identifiants valides existe
		When L'utilisateur appelle POST /api/auth/login avec les identifiants
		Then Un JWT valide est retourné dans la réponse
			And Le JWT peut être décodé sans erreur

	Scenario: L'endpoint refresh renouvelle le token
		Given L'utilisateur possède un JWT valide
			And L'API AquaPlan est en cours d'exécution
		When L'utilisateur appelle POST /api/auth/refresh avec le token
		Then Un nouveau JWT valide est retourné

	Scenario: Un utilisateur non authentifié est redirigé vers la page de connexion
		Given L'application Angular est en cours d'exécution
			And L'utilisateur n'est pas authentifié
		When L'utilisateur tente d'accéder à une page protégée
		Then L'utilisateur est redirigé vers la page de connexion

	Scenario: Le JWT contient les claims tenant et rôle
		Given L'utilisateur est authentifié
		When L'utilisateur consulte le JWT
		Then Le JWT contient le claim tenant
			And Le JWT contient le claim rôle
