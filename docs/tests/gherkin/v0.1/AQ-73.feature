Feature: Configurer NSwag

	Scenario: Swagger UI est accessible
		Given L'API AquaPlan est en cours d'exécution
		When L'utilisateur accède à http://localhost:5000/swagger
		Then Swagger UI est affiché
			And La liste des contrôleurs et endpoints est visible

	Scenario: NSwag génère les clients TypeScript automatiquement
		Given Le projet AquaPlan.Api est compilé en mode Debug
			And NSwag est configuré dans le fichier nswag.json
		When L'utilisateur déclenche la génération des clients
		Then Le fichier api-services.service.generated.ts est généré
			And Les clients TypeScript contiennent les endpoints de l'API
