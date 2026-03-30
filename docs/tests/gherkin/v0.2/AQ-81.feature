Feature: Création d'un compte utilisateur

	Scenario: L'administrateur crée un utilisateur avec toutes les informations requises
		Given L'utilisateur est authentifié en tant qu'administrateur
			And La page de gestion des utilisateurs est affichée
		When L'administrateur crée un nouvel utilisateur avec un email, un prénom et un nom
		Then L'utilisateur est créé avec succès
			And Un message de confirmation est affiché

	Scenario: Un email de bienvenue est envoyé au nouvel utilisateur
		Given Un nouvel utilisateur a été créé
		When L'administrateur confirme la création de l'utilisateur
		Then Un email de bienvenue est envoyé à l'adresse email de l'utilisateur
			And L'email contient un lien pour définir le mot de passe

	Scenario: La création échoue si l'email est déjà utilisé
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur avec l'email "test@example.com" existe
		When L'administrateur tente de créer un utilisateur avec l'email "test@example.com"
		Then Un message d'erreur est affiché
			And L'utilisateur n'est pas créé
