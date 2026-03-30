Feature: Configurer PostgreSQL + EF Core

	Scenario: Le DbContext se connecte à PostgreSQL
		Given PostgreSQL est en cours d'exécution sur le port 5432
			And La base de données AquaPlan existe
		When L'application lance une requête vers la base via EF Core
		Then La connexion réussit sans erreur

	Scenario: La migration initiale crée les tables User et Tenant
		Given Le DbContext AquaPlan est configuré
			And Les migrations EF Core sont appliquées
		When L'utilisateur vérifie le schéma de la base de données
		Then La table User existe avec les colonnes requises
			And La table Tenant existe avec les colonnes requises

	Scenario: Les tests d'intégration du DbContext passent
		Given Le projet AquaPlan.Infrastructure.Tests est compilé
		When L'utilisateur exécute les tests d'intégration EF Core
		Then Tous les tests passent
