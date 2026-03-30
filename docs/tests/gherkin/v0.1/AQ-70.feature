Feature: Initialiser la solution .NET

	Scenario: La solution compile sans erreurs
		Given Le projet AquaPlan.sln est ouvert
		When L'utilisateur compile la solution en mode Debug
		Then La compilation réussit sans erreurs

	Scenario: Les 7 projets existent avec les bonnes références
		Given Le projet AquaPlan.sln est ouvert
		When L'utilisateur consulte la structure des projets
		Then Les projets AquaPlan.Api, AquaPlan.Web, AquaPlan.Worker existent
			And Les projets AquaPlan.Domain, AquaPlan.Application, AquaPlan.Infrastructure, AquaPlan.Shared existent
			And Les dépendances entre projets sont correctes

	Scenario: Program.cs démarre sans exceptions
		Given Le projet AquaPlan.Api est compilé avec succès
		When L'utilisateur lance l'API en mode Debug
		Then L'API démarre sans exceptions
			And L'API écoute sur le port 5000
