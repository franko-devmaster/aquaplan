Feature: Affectation des permissions via rôles prédéfinis

	Scenario: Le système dispose de 4 rôles prédéfinis
		Given L'administrateur accède à la gestion des rôles
		When L'administrateur consulte la liste des rôles
		Then Le rôle Requérant existe
			And Le rôle Préleveur existe
			And Le rôle Requérant-Préleveur existe
			And Le rôle Administrateur existe

	Scenario: Chaque rôle a un ensemble de permissions prédéfinies
		Given L'administrateur consulte la configuration d'un rôle
		When L'administrateur accède aux détails du rôle Requérant
		Then Le rôle Requérant dispose des permissions pour créer des mandats
			And Le rôle Requérant dispose des permissions pour consulter les mandats

	Scenario: Les permissions contrôlent l'accès aux menus et actions
		Given Un utilisateur a le rôle Préleveur
		When L'utilisateur accède à l'interface AquaPlan
		Then Les menus accessibles correspondant au rôle Préleveur sont affichés
			And Les actions autorisées pour le rôle sont disponibles
			And Les actions non autorisées sont désactivées ou masquées
