Feature: Mettre en place le CI/CD

	Scenario: Le pipeline Bitbucket build le projet .NET
		Given Un pipeline Bitbucket Pipelines est configuré
			And Le fichier bitbucket-pipelines.yml existe
		When Une modification est pushée sur la branche main
		Then Le pipeline déclenche le build .NET
			And Le build exécute dotnet build

	Scenario: Le pipeline build le projet Angular
		Given Le pipeline Bitbucket Pipelines est configuré
		When Une modification est pushée sur la branche main
		Then Le pipeline déclenche le build Angular
			And Le build exécute ng build

	Scenario: Les tests sont exécutés dans le pipeline
		Given Le pipeline Bitbucket Pipelines est configuré
		When Une modification est pushée sur la branche main
		Then Les tests .NET s'exécutent avec xUnit
			And La couverture de code est rapportée
