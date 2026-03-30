Feature: Désactivation d'un compte utilisateur

	Scenario: L'administrateur désactive un compte
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur actif est affiché
		When L'administrateur désactive le compte utilisateur
		Then Le compte est marqué comme désactivé
			And Un message de confirmation est affiché

	Scenario: L'utilisateur désactivé ne peut plus se connecter
		Given Un utilisateur a été désactivé
		When L'utilisateur tente de se connecter avec ses identifiants
		Then L'authentification échoue
			And Un message indiquant que le compte est désactivé est affiché

	Scenario: Les données de l'utilisateur sont conservées
		Given Un utilisateur a été désactivé
		When L'administrateur consulte l'historique de l'utilisateur
		Then Toutes les données historiques de l'utilisateur sont conservées
			And Les mandats créés par cet utilisateur restent visibles dans l'audit
