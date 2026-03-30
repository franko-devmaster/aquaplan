Feature: Accès préleveur limité à ses mandats

	Scenario: Le préleveur ne voit que les mandats qui lui sont attribués
		Given L'utilisateur est authentifié avec le rôle Préleveur
			And Plusieurs mandats existent dans le système
			And Seul un mandat est attribué à cet utilisateur
		When L'utilisateur accède à la liste des mandats
		Then Seul le mandat attribué est affiché
			And Les mandats attribués à d'autres préleveurs ne sont pas visibles

	Scenario: La validation se fait côté serveur
		Given Un préleveur tente d'accéder aux données d'un mandat non attribué
		When Le préleveur envoie une requête GET pour consulter ce mandat
		Then L'API rejette la demande
			And Un code d'erreur 403 (Forbidden) est retourné
